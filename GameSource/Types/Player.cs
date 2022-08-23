using BrokeProtocol.API;
using BrokeProtocol.CustomEvents;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class GameSourcePlayer
    {
        ShPlayer player;

        public Vector3 goToPosition;
        public Quaternion goToRotation;
        public Transform goToParent;

        public float lastAlertTime;

        public GameSourcePlayer(ShPlayer player)
        {
            this.player = player;
        }

        public bool IsOffOrigin => player.GetParent != goToParent || player.DistanceSqr(goToPosition) > player.GetMount.svMountable.WaypointRangeSqr;

        
        public bool SetAttackState(ShEntity target)
        {
            if (target == player.svPlayer.leader) player.svPlayer.ClearLeader();

            player.svPlayer.targetEntity = target;

            bool returnState = false;

            if (player.GetControlled is ShAircraft aircraft)
            {
                if (aircraft.HasWeapons)
                    returnState = player.svPlayer.SetState(Core.AirAttack.index);
            }
            else returnState = player.svPlayer.SetState(Core.Attack.index);

            if (!returnState) player.svPlayer.targetEntity = null;

            return returnState;
        }

        public bool SetFollowState(ShPlayer leader)
        {
            player.svPlayer.leader = leader;
            player.svPlayer.targetEntity = leader;
            leader.svPlayer.follower = player;

            if (!player.svPlayer.SetState(Core.Follow.index))
            {
                player.svPlayer.ClearLeader();
                return false;
            }

            return true;
        }

        public void SetGoToState(Vector3 position, Quaternion rotation, Transform parent)
        {
            goToPosition = position;
            goToRotation = rotation;
            goToParent = parent;

            player.svPlayer.SetState(Core.GoTo.index);
        }
    }

    
    public class Player : PlayerEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Initialize(ShEntity entity)
        {
            Parent.Initialize(entity);
            Manager.pluginPlayers.Add(entity, new GameSourcePlayer(entity as ShPlayer));
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Destroy(ShEntity entity)
        {
            Parent.Destroy(entity);
            Manager.pluginPlayers.Remove(entity);
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Spawn(ShEntity entity)
        {
            Parent.Spawn(entity);
            if (entity is ShPlayer player)
            {
                player.StartCoroutine(Maintenance(player));
            }
            return true;
        }

        public IEnumerator Maintenance(ShPlayer player)
        {
            yield return null;

            var delay = new WaitForSeconds(5f);

            while (!player.IsDead)
            {
                if (player.GetStanceIndex == StanceIndex.Sleep)
                {
                    player.svPlayer.UpdateStatsDelta(0f, 0f, 0.02f);
                }
                else
                {
                    var injuryDelta = 0.05f / Util.hundredF;
                    var stomachLoss = player.injuryAmount[(int)BodyPart.Abdomen] * injuryDelta;
                    var energyLoss = player.injuryAmount[(int)BodyPart.Chest] * injuryDelta;
                    const float statsDelta = -0.0018f;
                    player.svPlayer.UpdateStatsDelta(statsDelta - stomachLoss, statsDelta - stomachLoss, statsDelta - energyLoss);
                }

                var totalDamage = 0f;
                foreach (var f in player.stats)
                {
                    if (f >= 0.75f) totalDamage -= 2f;
                    else if (f == 0f) totalDamage += 2f;
                }

                if (totalDamage > 0f) player.svPlayer.Damage(DamageIndex.Null, totalDamage);
                else if (totalDamage < 0f && player.health < player.maxStat * 0.5f) player.svPlayer.SvHeal(-totalDamage);

                if (player.isHuman)
                {
                    if (player.otherEntity && (!player.otherEntity.isActiveAndEnabled || !player.InActionRange(player.otherEntity)))
                    {
                        player.svPlayer.SvStopInventory(true);
                    }
                }
                else if (Random.value < 0.1f)
                {
                    if (player.injuries.Count > 0)
                    {
                        player.svPlayer.SvConsume(SceneManager.Instance.healerCollection.GetRandom().Value);
                    }
                    else
                    {
                        player.svPlayer.SvConsume(SceneManager.Instance.consumablesCollection.GetRandom().Value);
                    }
                }

                yield return delay;
            }
        }

        

        [Execution(ExecutionMode.Additive)]
        public override bool GlobalChatMessage(ShPlayer player, string message)
        {
            if (Utility.chatted.Limit(player)) return true;

            message = message.CleanMessage();

            if (string.IsNullOrWhiteSpace(message)) return true;

            Debug.Log($"[CHAT] {player.username}:{message}");

            // 'true' if message starts with command prefix
            if (CommandHandler.OnEvent(player, message)) return true;

            player.svPlayer.Send(SvSendType.All, Channel.Reliable, ClPacket.GlobalChatMessage, player.ID, message);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool LocalChatMessage(ShPlayer player, string message)
        {
            if (Utility.chatted.Limit(player)) return true;

            message = message.CleanMessage();

            if (string.IsNullOrWhiteSpace(message)) return true;

            Debug.Log($"[CHAT] {player.username}:{message}");

            // 'true' if message starts with command prefix
            if (CommandHandler.OnEvent(player, message)) return true;

            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.LocalChatMessage, player.ID, message);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Damage(ShDestroyable destroyable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 source, Vector3 hitPoint)
        {
            if (!(destroyable is ShPlayer player) || player.svPlayer.godMode || player.IsDead || player.IsShielded(damageIndex, collider)) return true;

            if (damageIndex != DamageIndex.Null)
            {
                BodyEffect effect;
                var random = Random.value;

                if (random < 0.6f)
                    effect = BodyEffect.Null;
                else if (random < 0.8f)
                    effect = BodyEffect.Pain;
                else if (random < 0.925f)
                    effect = BodyEffect.Bloodloss;
                else
                    effect = BodyEffect.Fracture;

                BodyPart part;

                var capsuleHeight = player.capsule.direction == 1 ? player.capsule.height : player.capsule.radius * 2f;

                var hitY = player.GetLocalY(hitPoint);

                if (damageIndex == DamageIndex.Random)
                {
                    part = (BodyPart)Random.Range(0, (int)BodyPart.Count);
                }
                else if (damageIndex == DamageIndex.Melee && player.IsBlocking(damageIndex))
                {
                    part = BodyPart.Arms;
                    amount *= 0.3f;
                }
                else if (collider == player.headCollider) // Headshot
                {
                    part = BodyPart.Head;
                    amount *= 2f;
                }
                else if (hitY >= capsuleHeight * 0.75f)
                {
                    part = Random.value < 0.5f ? BodyPart.Arms : BodyPart.Chest;
                }
                else if (hitY >= capsuleHeight * 0.5f)
                {
                    part = BodyPart.Abdomen;
                    amount *= 0.8f;
                }
                else
                {
                    part = BodyPart.Legs;
                    amount *= 0.5f;
                }

                if (effect != BodyEffect.Null)
                {
                    player.svPlayer.SvAddInjury(part, effect, (byte)Random.Range(10, 50));
                }
            }

            if (!player.isHuman)
            {
                amount /= SvManager.Instance.settings.difficulty;
            }

            amount -= amount * (player.armorLevel / 200f);

            Parent.Damage(player, damageIndex, amount, attacker, collider, source, hitPoint);

            if (player.IsDead) return true;

            // Still alive, do knockdown and AI retaliation

            if (player.stance.setable)
            {
                if (player.isHuman && player.health < 15f)
                {
                    player.svPlayer.SvForceStance(StanceIndex.KnockedOut);
                    // If knockout AI, set AI state Null
                }
                else if (Random.value < player.manager.damageTypes[(int)damageIndex].fallChance)
                {
                    player.StartCoroutine(player.svPlayer.KnockedDown());
                }
            }

            if (attacker && attacker != player)
            {
                if (!player.isHuman && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                {
                    pluginPlayer.SetAttackState(attacker);
                }
                else if(player.svPlayer.follower && Manager.pluginPlayers.TryGetValue(player.svPlayer.follower, out var pluginPlayerFollower))
                {
                    pluginPlayerFollower.SetAttackState(attacker);
                }

                if (attacker.svPlayer.follower && Manager.pluginPlayers.TryGetValue(attacker.svPlayer.follower, out var pluginAttackerFollower))
                {
                    pluginAttackerFollower.SetAttackState(player);
                }
            }

            return true;
        }

        private IEnumerator SpectateDelay(ShPlayer player, ShPlayer target)
        {
            yield return new WaitForSeconds(2f);
            player.svPlayer.SvSpectate(target);
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Death(ShDestroyable destroyable, ShPlayer attacker)
        {
            if (!(destroyable is ShPlayer player)) return true;

            if (attacker && attacker != player)
            {
                if (player.isHuman) player.StartCoroutine(SpectateDelay(player, attacker));

                player.svPlayer.RemoveItemsDeath(true);
            }
            else
            {
                player.svPlayer.RemoveItemsDeath(false);
            }

            player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.ShowTimer, player.svPlayer.RespawnTime);

            player.SetStance(StanceIndex.Dead);

            Parent.Death(destroyable, attacker);

            return true;
        }

        
        [Execution(ExecutionMode.Additive)]
        public override bool Respawn(ShEntity entity)
        {
            if (!(entity is ShPlayer player)) return true;

            if (player.isHuman)
            {
                var newSpawn = Manager.spawnLocations.GetRandom().mainT;
                player.svPlayer.originalPosition = newSpawn.position;
                player.svPlayer.originalRotation = newSpawn.rotation;
                player.svPlayer.originalParent = newSpawn.parent;
            }

            Parent.Respawn(player);

            if (player.isHuman)
            {
                // Back to spectate self on Respawn
                player.svPlayer.SvSpectate(player);
            }

            player.svPlayer.SvForceEquipable(player.Hands.index);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Reward(ShPlayer player, int experienceDelta, int moneyDelta)
        {
            if (!player.isHuman) return true;

            // Player rank affects money rewards (can adjust for modding)
            moneyDelta *= player.rank + 1;

            if (moneyDelta > 0)
            {
                player.TransferMoney(DeltaInv.AddToMe, moneyDelta);
            }
            else if (moneyDelta < 0)
            {
                player.TransferMoney(DeltaInv.RemoveFromMe, -moneyDelta);
            }

            if (player.svPlayer.job.info.upgrades.Length <= 1) return true;

            var experience = player.experience + experienceDelta;

            if (experience > Util.maxExperience)
            {
                if (player.rank >= player.svPlayer.job.info.upgrades.Length - 1)
                {
                    if (player.experience != Util.maxExperience)
                    {
                        player.svPlayer.SetExperience(Util.maxExperience, true);
                    }
                }
                else
                {
                    var newRank = player.rank + 1;
                    player.svPlayer.AddJobItems(player.svPlayer.job.info, player.rank, newRank, false);
                    player.svPlayer.SetRank(newRank);
                    player.svPlayer.SetExperience(experience - Util.maxExperience, false);
                }
            }
            else if (experience <= 0)
            {
                if (player.rank <= 0)
                {
                    player.svPlayer.SendGameMessage("You lost your job");
                    player.svPlayer.SvResetJob();
                }
                else
                {
                    player.svPlayer.SetRank(player.rank - 1);
                    player.svPlayer.SetExperience(experience + Util.maxExperience, false);
                }
            }
            else
            {
                player.svPlayer.SetExperience(experience, true);
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Collect(ShPlayer player, ShEntity e, bool consume)
        {
            if (consume && e is ShConsumable consumable)
            {
                if (!player.svPlayer.SvConsume(consumable)) return true;
            }
            else
            {
                var collectedItems = e.CollectedItems;

                for (var i = 0; i < collectedItems.Length; i++)
                {
                    var inv = collectedItems[i];

                    var itemIndex = inv.itemName.GetPrefabIndex();
                    if (SceneManager.Instance.TryGetEntity<ShItem>(itemIndex, out var item))
                    {
                        player.svPlayer.CollectItem(item, inv.count);
                        if (i == 0) player.svPlayer.SvTrySetEquipable(itemIndex);
                    }
                }
            }

            e.Destroy();

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Bomb(ShPlayer player, ShVault vault)
        {
            player.TransferItem(DeltaInv.RemoveFromMe, ShManager.Instance.bomb.index);

            player.svPlayer.SvShowTimer(vault.svVault.bombTimer);

            vault.svVault.SvSetVault(VaultState.Bombing);
            vault.svVault.instigator = player;

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Repair(ShPlayer player, ShTransport transport)
        {
            if (transport.svTransport.SvHeal(transport.maxStat, player))
                player.TransferItem(DeltaInv.RemoveFromMe, ShManager.Instance.toolkit);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Lockpick(ShPlayer player, ShTransport transport)
        {
            if (player.CanMount(transport, false, true, out _) && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                player.TransferItem(DeltaInv.RemoveFromMe, ShManager.Instance.lockpick);
                transport.state = EntityState.Unlocked;
                transport.svTransport.SendTransportState();

                player.svPlayer.SvTryMount(transport.ID, true);
            }

            return true;
        }


        [Execution(ExecutionMode.Additive)]
        public override bool Injury(ShPlayer player, BodyPart part, BodyEffect effect, byte amount)
        {
            player.AddInjury(part, effect, amount);
            player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.AddInjury, (byte)part, (byte)effect, amount);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Kick(ShPlayer player, ShPlayer target, string reason)
        {
            ChatHandler.SendToAll($"{target.displayName} Kicked: {reason}");

            player.manager.svManager.KickConnection(target.svPlayer.connection);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Ban(ShPlayer player, ShPlayer target, string reason)
        {
            ChatHandler.SendToAll($"{target.displayName} Banned: {reason}");

            player.svPlayer.SvBanDatabase(target.username, reason);
            player.manager.svManager.Disconnect(target.svPlayer.connection, DisconnectTypes.Banned);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool RemoveItemsDeath(ShPlayer player, bool dropItems)
        {
            var removedItems = new List<InventoryItem>();

            // Allows players to keep items/rewards from job ranks
            foreach (var myItem in player.myItems.Values.ToArray())
            {
                int extra = myItem.count;

                if (player.svPlayer.job.info.upgrades.Length > player.rank)
                {
                    for (int rankIndex = player.rank; rankIndex >= 0; rankIndex--)
                    {
                        foreach (var i in player.svPlayer.job.info.upgrades[rankIndex].items)
                        {
                            if (myItem.item.name == i.itemName)
                            {
                                extra = Mathf.Max(0, myItem.count - i.count);
                            }
                        }
                    }
                }

                // Remove everything except legal items currently worn
                if (extra > 0 && (myItem.item.illegal || !(myItem.item is ShWearable w) || player.curWearables[(int)w.type].index != w.index))
                {
                    removedItems.Add(new InventoryItem(myItem.item, extra));
                    player.TransferItem(DeltaInv.RemoveFromMe, myItem.item.index, extra, true);
                }
            }

            // Only drop items if attacker present, to prevent AI suicide item farming
            if (dropItems && removedItems.Count > 0)
            {
                var briefcase = player.svPlayer.SpawnBriefcase();

                if (briefcase)
                {
                    foreach (var invItem in removedItems)
                    {
                        if (Random.value < 0.8f)
                        {
                            invItem.count = Mathf.CeilToInt(invItem.count * Random.Range(0.05f, 0.3f));
                            briefcase.myItems.Add(invItem.item.index, invItem);
                        }
                    }
                }
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Restrain(ShPlayer player, ShPlayer initiator, ShRestrained restrained)
        {
            if (player.svPlayer.godMode) return true;

            if (player.curMount) player.svPlayer.SvDismount();

            player.svPlayer.SvSetEquipable(restrained);

            if (!player.isHuman)
            {
                player.svPlayer.SetState(Core.Restrained.index);
            }
            else
            {
                player.svPlayer.SendGameMessage("You've been restrained");
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Unrestrain(ShPlayer player, ShPlayer initiator)
        {
            player.svPlayer.SvSetEquipable(player.Hands);

            if (!player.isHuman)
            {
                player.svPlayer.SvDismount();
                player.svPlayer.ResetAI();
            }
            else
            {
                player.svPlayer.SendGameMessage("You've been freed");
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool ServerInfo(ShPlayer player)
        {
            player.svPlayer.SendTextMenu("&7Server Info", SvManager.Instance.serverInfo);
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool DisplayName(ShPlayer player, string username)
        {
            player.username = username;

            if (player.isHuman)
            {
                player.svPlayer.tagname = player.svPlayer.PrimaryGroup?.Tag ?? string.Empty;
            }
            else
            {
                player.svPlayer.tagname = string.Empty;
            }

            // &f to reset back to white after any tag colors
            player.displayName = $"{player.svPlayer.tagname}&f{player.username}";

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool EnterDoor(ShPlayer player, ShDoor door, ShPlayer sender, bool forceEnter)
        {
            if (!forceEnter)
            {
                if (!IsDoorAccessible(player, door) || player.IsRestrained || !player.InActionRange(door))
                    return true;
            }

            ShMountable baseEntity;

            if (player.curMount is ShPlayer mountPlayer)
            {
                baseEntity = mountPlayer;
            }
            else
            {
                baseEntity = player;
            }

            if (door is ShApartment apartment && sender.ownedApartments.TryGetValue(apartment, out var place))
            {
                baseEntity.svMountable.SvRelocate(place.mainDoor.spawnPoint, place.mTransform);
            }
            else
            {
                var otherDoor = door.svDoor.other;
                baseEntity.svMountable.SvRelocate(otherDoor.spawnPoint, otherDoor.GetPlace.mTransform);
            }

            return true;
        }


        [Execution(ExecutionMode.Additive)]
        public override bool Follower(ShPlayer player, ShPlayer other)
        {
            if (player.svPlayer.follower)
            {
                if (player.svPlayer.follower != other)
                {
                    player.svPlayer.SendGameMessage("Already have a follower");
                }
                else
                {
                    other.svPlayer.SvDismount();
                    other.svPlayer.ClearLeader();
                    other.svPlayer.ResetAI();
                }
            }
            else if (!other.svPlayer.leader && other.CanFollow && !other.svPlayer.currentState.IsBusy && 
                Manager.pluginPlayers.TryGetValue(other, out var pluginOther))
            {
                pluginOther.SetFollowState(player);
            }
            else
            {
                player.svPlayer.SendGameMessage("NPC is occupied");
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool TextPanelButton(ShPlayer player, string menuID, string optionID)
        {
            if (menuID.StartsWith(ExampleCommand.coinFlip))
            {
                switch (optionID)
                {
                    case ExampleCommand.heads:
                        player.StartCoroutine(DelayCoinFlip(player, ExampleCommand.heads, menuID));
                        break;
                    case ExampleCommand.tails:
                        player.StartCoroutine(DelayCoinFlip(player, ExampleCommand.tails, menuID));
                        break;
                    case ExampleCommand.cancel:
                        player.svPlayer.DestroyTextPanel(menuID);
                        break;
                }
            }

            return true;
        }

        private IEnumerator DelayCoinFlip(ShPlayer player, string prediction, string menuID)
        {
            const int coinFlipCost = 100;
            var delay = new WaitForSeconds(1f);

            player.svPlayer.SendTextPanel("Flipping coin..", menuID);
            yield return delay;

            if (player.MyMoneyCount < coinFlipCost)
            {
                player.svPlayer.SendTextPanel($"Need ${coinFlipCost} to play", menuID);
            }
            else
            {
                var coin = Random.value >= 0.5f ? ExampleCommand.heads : ExampleCommand.tails;

                if (coin == prediction)
                {
                    player.svPlayer.SendTextPanel($"Flipped {coin}!\n\n&aYou guessed right!", menuID);
                    player.TransferMoney(DeltaInv.AddToMe, coinFlipCost);
                }
                else
                {
                    player.svPlayer.SendTextPanel($"Flipped {coin}!\n\n&4You guessed wrong :(", menuID);
                    player.TransferMoney(DeltaInv.RemoveFromMe, coinFlipCost);
                }
            }

            yield return delay;
            player.svPlayer.DestroyTextPanel(menuID);
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Ready(ShPlayer player)
        {
            player.svPlayer.SendServerInfo();

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Point(ShPlayer player, bool pointing)
        {
            player.pointing = pointing;
            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.Point, player.ID, pointing);

            if (pointing && player.svPlayer.follower &&
                Physics.Raycast(player.GetOrigin, player.GetRotationT.forward, out var hit, Util.visibleRange, MaskIndex.hard) &&
                player.svPlayer.follower.svPlayer.NodeNear(hit.point) != null &&
                Manager.pluginPlayers.TryGetValue(player.svPlayer.follower, out var pluginFollower))
            {
                pluginFollower.SetGoToState(hit.point, Quaternion.LookRotation(hit.point - player.svPlayer.follower.GetPosition), player.GetParent);
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Alert(ShPlayer player)
        {
            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.Alert, player.ID);
            if (player.svPlayer.follower && Manager.pluginPlayers.TryGetValue(player.svPlayer.follower, out var pluginFollower))
            {
                pluginFollower.SetFollowState(player);
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool DestroySelf(ShDestroyable destroyable)
        {
            if (destroyable is ShPlayer player && !(player.isHuman && player.IsRestrained && player.IsUp))
                Parent.DestroySelf(player);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool SetEquipable(ShPlayer player, ShEquipable equipable)
        {
            if (!player.curEquipable || player.curEquipable.index != equipable.index)
            {
                player.svPlayer.SvForceEquipable(equipable.index);
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Mount(ShPlayer player, ShMountable mount, byte seat)
        {
            player.svPlayer.SvDismount();
            player.Mount(mount, seat);
            player.SetStance(mount.seats[seat].stanceIndex);

            if (!player.isHuman)
            {
                player.svPlayer.ResetAI();
            }

            player.svPlayer.Send(SvSendType.Local, Channel.Reliable, ClPacket.Mount, player.ID, mount.ID, seat, mount.CurrentClip);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Dismount(ShPlayer player)
        {
            if (player.IsDriving)
            {
                // Send serverside transport position to override client-side predicted location while it was driven
                player.curMount.svMountable.SvRepositionSelf();
            }

            player.SetStance(StanceIndex.Stand);
            player.Dismount();

            // Start locking behavior after exiting vehicle
            if (player.curEquipable.ThrownHasGuidance)
            {
                player.svPlayer.StartLocking(player.curEquipable);
            }

            player.svPlayer.Send(SvSendType.Local, Channel.Reliable, ClPacket.Dismount, player.ID);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool PlaceItem(ShPlayer player, ShEntity placeableEntity, Vector3 position, Quaternion rotation, float spawnDelay)
        {
            if (spawnDelay > 0f)
            {
                SvManager.Instance.AddNewEntityDelay(
                    placeableEntity,
                    player.GetPlace,
                    position,
                    rotation,
                    Vector3.one,
                    false,
                    placeableEntity.data,
                    spawnDelay);
            }
            else
            {
                SvManager.Instance.AddNewEntity(
                    placeableEntity,
                    player.GetPlace,
                    position,
                    rotation,
                    false);
            }

            player.svPlayer.placementValid = true;

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool ResetAI(ShPlayer player)
        {
            if (player.svPlayer.targetPlayer && !player.svPlayer.preFrame)
            {
                player.svPlayer.targetPlayer = null;
                player.svPlayer.Respawn();
            }
            else if(Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                if (player.IsKnockedOut && player.svPlayer.SetState(Core.Null.index)) return true;
                if (player.IsRestrained && player.svPlayer.SetState(Core.Restrained.index)) return true;
                player.svPlayer.SvTrySetEquipable(player.Hands.index);
                if (player.svPlayer.leader && pluginPlayer.SetFollowState(player.svPlayer.leader)) return true;
                if (player.IsPassenger && player.svPlayer.SetState(Core.Null.index)) return true;

                player.svPlayer.targetEntity = null;

                if (player.IsDriving && player.svPlayer.SetState(Core.Waypoint.index)) return true;
                if (player.svPlayer.currentState.index == Core.Freeze.index && !player.svPlayer.stop && player.svPlayer.SetState(Core.Flee.index)) return true;
                if (player.svPlayer.targetPlayer && pluginPlayer.SetAttackState(player.svPlayer.targetPlayer)) return true;

                if (player.GetParent != player.svPlayer.originalParent)
                {
                    player.svPlayer.ResetOriginal();
                }

                player.svPlayer.job.ResetJobAI();
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool RestrainOther(ShPlayer player, ShPlayer hitPlayer, ShRestraint restraint)
        {
            restraint.RemoveAmmo();

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Park(ShPlayer player, ShTransport transport)
        {
            if (transport && !transport.IsDead)
            {
                if (transport.controller && transport.controller != player)
                {
                    player.svPlayer.SendGameMessage("Transport is occupied");
                    return true;
                }

                var distance = Mathf.Infinity;
                ShDoor garage = null;
                foreach (var d in transport.svTransport.GetLocalInRange<ShDoor>(Util.inviteDistance))
                {
                    var tempDistance = transport.DistanceSqr(d);
                    if (d.isGarage && tempDistance < distance)
                    {
                        distance = tempDistance;
                        garage = d;
                    }
                }

                if (garage)
                {
                    if (!IsDoorAccessible(player, garage))
                        return true;

                    if (garage is ShApartment apartment && player.ownedApartments.TryGetValue(apartment, out var place))
                    {
                        if (ShManager.Instance.PlaceItemCount(place) >= apartment.limit ||
                            !transport.svTransport.TryParking(place.mainDoor))
                        {
                            player.svPlayer.SendGameMessage("Cannot park in private garage");
                        }
                    }
                    else if (!transport.svTransport.TryParking(garage.svDoor.other))
                    {
                        player.svPlayer.SendGameMessage("Cannot park in garage");
                    }
                }
                else
                {
                    player.svPlayer.SendGameMessage("No garage door nearby");
                }
            }

            return true;
        }

        private bool IsDoorAccessible(ShPlayer player, ShDoor door)
        {
            if (door.svDoor.key && !player.HasItem(door.svDoor.key))
            {
                player.svPlayer.SendGameMessage("Need " + door.svDoor.key.itemName + " to enter");
                return false;
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool TransferShop(ShPlayer player, byte deltaType, int itemIndex, int amount)
        {
            int multiplier;
            bool markup;
            ShEntity source;
            ShEntity target;

            var shop = player.otherEntity;

            if (!shop || !shop.Shop || !SceneManager.Instance.TryGetEntity<ShItem>(itemIndex, out var item) || !shop.ShopCanBuy(item))
            {
                player.svPlayer.SendGameMessage("Not suitable");
                return true;
            }

            if (deltaType == DeltaInv.MeToOther)
            {
                multiplier = 1;
                markup = false;
                source = player;
                target = shop;
            }
            else
            {
                multiplier = -1;
                markup = true;
                source = shop;
                target = player;
            }

            var totalTransferValue = 0;
            var initialItemCount = shop.MyItemCount(itemIndex);

            for (var count = 0; count < amount; count++)
            {
                totalTransferValue += item.GetValue(initialItemCount + multiplier * count, markup);
            }

            if (source.MyItemCount(itemIndex) >= amount && target.MyMoneyCount >= totalTransferValue)
            {
                player.TransferItem(deltaType, itemIndex, amount);
                player.TransferMoney(DeltaInv.InverseDelta[deltaType], totalTransferValue, true);
            }
            else
            {
                player.svPlayer.SendGameMessage("Unable");
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool TransferTrade(ShPlayer player, byte deltaType, int itemIndex, int amount)
        {
            if (!(player.otherEntity is ShPlayer otherPlayer)) return true;

            player.TransferItem(deltaType, itemIndex, amount);

            if (!otherPlayer.isHuman)
            {
                var itemValue = 0;

                foreach (var pair in player.tradeItems)
                {
                    if (otherPlayer.svPlayer.buyerType.type.IsInstanceOfType(pair.Value.item))
                    {
                        var value = pair.Value.item.GetValue(otherPlayer.MyItemCount(itemIndex) + player.TradeItemCount(itemIndex), false);

                        itemValue += pair.Value.count * value;
                    }
                }

                var difference = Mathf.Min(itemValue - otherPlayer.TradeMoneyCount, otherPlayer.MyMoneyCount);

                if (difference > 0) otherPlayer.TransferMoney(DeltaInv.MeToTrade, difference, true);
                else if (difference < 0) otherPlayer.TransferMoney(DeltaInv.TradeToMe, -difference, true);
            }

            return true;
        }
    }
}