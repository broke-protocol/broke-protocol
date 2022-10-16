using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
using BrokeProtocol.Utility.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BrokeProtocol.GameSource.Types
{
    public class GameSourcePlayer
    {
        ShPlayer player;

        public Coroutine jobCoroutine;

        public float lastAlertTime;

        public GameSourcePlayer(ShPlayer player)
        {
            this.player = player;
        }
        
        public virtual bool SetAttackState(ShEntity target)
        {
            // Sanity check
            if (!target || target == player)
                return false;

            if (target == player.svPlayer.leader)
                player.svPlayer.ClearLeader();

            var previousTarget = player.svPlayer.targetEntity;

            player.svPlayer.targetEntity = target;

            State attackState = null;

            if (player.curMount)
            {
                if (player.IsFlying)
                {
                    if (player.curMount.HasWeapons)
                    {
                        attackState = Core.AirAttack;
                    }
                }
                else if (player.curMount is ShMovable && player.IsSeatedFirst)
                {
                    attackState = Core.Attack;
                }
                else if (player.svPlayer.SetState(Core.StaticAttack.index))
                {
                    attackState = Core.StaticAttack;
                }
            }
            else if (player.svPlayer.SetState(Core.Attack.index))
            {
                attackState = Core.Attack;
            }

            if(attackState != null && 
                (target != previousTarget || player.svPlayer.currentState != attackState) && 
                player.svPlayer.SetState(attackState.index))
            {
                return true;
            }

            // Restore previous target on fail
            player.svPlayer.targetEntity = previousTarget;
            return false;
        }

        public virtual bool SetFollowState(ShPlayer leader)
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

        public virtual bool SetGoToState(Vector3 position, Quaternion rotation, Transform parent)
        {
            player.svPlayer.destinationPosition = position;
            player.svPlayer.destinationRotation = rotation;
            player.svPlayer.destinationParent = parent;

            return player.svPlayer.SetState(Core.GoTo.index);
        }

        public virtual bool MountWithinReach(ShEntity target)
        {
            var m = player.GetMount;
            return m && m.Velocity.sqrMagnitude <= Utility.slowSpeedSqr && target.InActionRange(m);
        }
    }


    public class Player : PlayerEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Initialize(ShEntity entity)
        {
            Manager.pluginPlayers.Add(entity, new GameSourcePlayer(entity.Player));
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Destroy(ShEntity entity)
        {
            Manager.pluginPlayers.Remove(entity);
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Spawn(ShEntity entity)
        {
            entity.StartCoroutine(Maintenance(entity.Player));
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
                else if (totalDamage < 0f && player.health < player.maxStat * 0.5f) player.svPlayer.Heal(-totalDamage);

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
            var player = destroyable.Player;
            if (destroyable.IsDead || player.svPlayer.godMode) return false;

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
                if (!player.isHuman)
                {
                    player.GamePlayer().SetAttackState(attacker);
                }
                else if (player.svPlayer.follower && attacker != player.svPlayer.follower)
                {
                    player.svPlayer.follower.GamePlayer().SetAttackState(attacker);
                }

                if (attacker.svPlayer.follower)
                {
                    attacker.svPlayer.follower.GamePlayer().SetAttackState(player);
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
            var player = destroyable.Player;

            if (attacker && attacker != player)
            {
                if (player.isHuman) player.StartCoroutine(SpectateDelay(player, attacker));

                player.svPlayer.RemoveItemsDeath(true);
            }
            else
            {
                player.svPlayer.RemoveItemsDeath(false);
            }

            if (player.isHuman)
            {
                player.svPlayer.SendTimer(player.svPlayer.RespawnTime, new Vector2(0.5f, 0.15f));
            }
            return true;
        }


        [Execution(ExecutionMode.Additive)]
        public override bool Respawn(ShEntity entity)
        {
            var player = entity.Player;
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

            if (player.svPlayer.job.info.shared.upgrades.Length <= 1) return true;

            while (experienceDelta != 0)
            {
                var previousMaxExperience = player.GetMaxExperience();

                var newExperience = Mathf.Clamp(player.experience + experienceDelta, -1, previousMaxExperience);

                experienceDelta -= newExperience - player.experience;

                if (newExperience >= previousMaxExperience)
                {
                    if (player.rank + 1 >= player.svPlayer.job.info.shared.upgrades.Length)
                    {
                        if (player.experience != previousMaxExperience)
                        {
                            player.svPlayer.SetExperience(previousMaxExperience, true);
                        }
                        return true;
                    }
                    else
                    {
                        var newRank = player.rank + 1;
                        player.svPlayer.AddJobItems(player.svPlayer.job.info, newRank, false);
                        player.svPlayer.SetRank(newRank);
                        player.svPlayer.SetExperience(newExperience - previousMaxExperience, false);
                    }
                }
                else if (newExperience < 0)
                {
                    if (player.rank <= 0)
                    {
                        player.svPlayer.SendGameMessage("You lost your job");
                        player.svPlayer.SvResetJob();
                        return true;
                    }
                    else
                    {
                        player.svPlayer.SetRank(player.rank - 1);
                        player.svPlayer.SetExperience(newExperience + player.GetMaxExperience(), false);
                    }
                }
                else
                {
                    player.svPlayer.SetExperience(newExperience, true);
                }
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

            player.svPlayer.SendTimer(vault.svVault.bombTimer, new Vector2(0.5f, 0.15f));

            vault.svVault.SvSetVault(VaultState.Bombing);
            vault.svVault.instigator = player;

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Repair(ShPlayer player, ShTransport transport)
        {
            if (transport.svTransport.HealFull(player))
            {
                player.TransferItem(DeltaInv.RemoveFromMe, ShManager.Instance.toolkit);
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Lockpick(ShPlayer player, ShTransport transport)
        {
            if (player.CanMount(transport, false, true, out _))
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
            InterfaceHandler.SendGameMessageToAll($"{target.displayName} Kicked: {reason}");

            SvManager.Instance.KickConnection(target.svPlayer.connection);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Ban(ShPlayer player, ShPlayer target, string reason)
        {
            InterfaceHandler.SendGameMessageToAll($"{target.displayName} Banned: {reason}");

            player.svPlayer.SvBanDatabase(target.username, reason);
            SvManager.Instance.Disconnect(target.svPlayer.connection, DisconnectTypes.Banned);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool RemoveItemsDeath(ShPlayer player, bool dropItems)
        {
            var removedItems = new List<InventoryItem>();

            // Allows players to keep items/rewards from job ranks
            foreach (var myItem in player.myItems.Values.ToArray())
            {
                var extra = myItem.count;

                var upgrades = player.svPlayer.job.info.shared.upgrades;

                if (upgrades.Length > player.rank)
                {
                    for (int rankIndex = player.rank; rankIndex >= 0; rankIndex--)
                    {
                        foreach (var i in upgrades[rankIndex].items)
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
                    player.TransferItem(DeltaInv.RemoveFromMe, myItem.item.index, extra);
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
                            invItem.count = Mathf.CeilToInt(invItem.count * Random.Range(0.1f, 0.4f));
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
                player.svPlayer.SvDismount(true);
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

                if(player.curMount)
                {
                    player.svPlayer.SvDismount();
                }
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
                    other.svPlayer.ClearLeader();
                    other.svPlayer.SvDismount(true);
                }
            }
            else if (!other.svPlayer.leader && other.CanFollow && !other.svPlayer.currentState.IsBusy)
            {
                other.GamePlayer().SetFollowState(player);
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
            if (menuID.StartsWith(Commands.coinFlip))
            {
                switch (optionID)
                {
                    case Commands.heads:
                        player.StartCoroutine(DelayCoinFlip(player, Commands.heads, menuID));
                        break;
                    case Commands.tails:
                        player.StartCoroutine(DelayCoinFlip(player, Commands.tails, menuID));
                        break;
                    case Commands.cancel:
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
                var coin = Random.value >= 0.5f ? Commands.heads : Commands.tails;

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
                player.svPlayer.follower.svPlayer.NodeNear(hit.point) != null)
            {
                player.svPlayer.follower.svPlayer.SvDismount();
                player.svPlayer.follower.GamePlayer().SetGoToState(hit.point, Quaternion.LookRotation(hit.point - player.svPlayer.follower.GetPosition), player.GetParent);
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Alert(ShPlayer player)
        {
            if (player.svPlayer.follower)
            {
                player.svPlayer.follower.svPlayer.ResetAI();
            }

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
            var pluginPlayer = player.GamePlayer();

            player.svPlayer.targetEntity = null;

            if (player.IsKnockedOut && player.svPlayer.SetState(Core.Null.index)) return true;
            if (player.IsRestrained && player.svPlayer.SetState(Core.Restrained.index)) return true;
            if (player.svPlayer.leader && pluginPlayer.SetFollowState(player.svPlayer.leader)) return true;
            player.svPlayer.SvTrySetEquipable(player.Hands.index);
            if (player.IsPassenger && player.svPlayer.SetState(Core.Look.index)) return true;

            if (player.svPlayer.spawnTarget && pluginPlayer.SetAttackState(player.svPlayer.spawnTarget)) return true;
            player.svPlayer.spawnTarget = null;
                
            player.svPlayer.job.ResetJobAI();

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

            if (deltaType == DeltaInv.MeToShop)
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

        private const string updateTextMenu = "UpdateTextMenu";

        [Execution(ExecutionMode.Additive)]
        public override bool UpdateTextDisplay(ShPlayer player, ShTextDisplay textDisplay)
        {
            if (textDisplay.editableLength > 0)
            {
                player.svPlayer.SendInputMenu(
                    $"Update Text (${textDisplay.value})",
                    textDisplay.ID,
                    updateTextMenu,
                    InputField.ContentType.Standard,
                    textDisplay.editableLength);
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool SubmitInput(ShPlayer player, int targetID, string id, string input)
        {
            switch (id)
            {
                case updateTextMenu:
                    var textDisplay = EntityCollections.FindByID<ShTextDisplay>(targetID);

                    if (textDisplay && textDisplay.editableLength > 0)
                    {
                        var text = input.Trim();

                        if (text.Length > 0 && text.Length <= textDisplay.editableLength)
                        {
                            if (player.MyMoneyCount >= textDisplay.value)
                            {
                                textDisplay.svTextDisplay.UpdateText(text);
                                player.TransferMoney(DeltaInv.RemoveFromMe, textDisplay.value);
                                InterfaceHandler.SendGameMessageToAll(player.username + " updated a Text Sign");
                            }
                            else
                            {
                                player.svPlayer.SendGameMessage("Insufficient Funds");
                            }
                        }
                        else
                        {
                            player.svPlayer.SendGameMessage("Invalid Input");
                        }
                    }
                    break;
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Consume(ShPlayer player, ShConsumable consumable, ShPlayer healer)
        {
            if (player.IsDead) return false;

            if (consumable.canRevive)
            {
                if (!healer) return false;

                if (player.IsKnockedOut)
                {
                    player.svPlayer.StartRecover();
                    healer.svPlayer.job.OnRevivePlayer(player);
                }
                else
                {
                    player.svPlayer.Damage(consumable.DamageProperty, 1f, healer);
                    return false; // Don't heal after hurting!
                }
            }

            if (consumable.healedEffects.Length > 0)
            {
                if (player.svPlayer.HealFromConsumable(consumable))
                {
                    if (healer) healer.svPlayer.job.OnHealEntity(player);
                }
                else if (healer)
                {
                    healer.svPlayer.SendGameMessage("No related injury");
                    return false;
                }
            }

            if (consumable.healthBoost > 0f)
            {
                if (player.CanHeal)
                {
                    if (healer && Utility.healed.Limit(player))
                    {
                        const string message = "Heal Limit Reached!";
                        player.svPlayer.SendGameMessage(message);
                        healer.svPlayer.SendGameMessage(message);
                        return false;
                    }

                    player.svPlayer.Heal(consumable.healthBoost, healer);
                }
                else if (healer) return false;
            }
            else if (consumable.healthBoost < 0f)
            {
                player.svPlayer.Damage(consumable.DamageProperty, -consumable.healthBoost, healer);
            }

            player.svPlayer.UpdateStatsAndRemoveConsumable(consumable);
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
    }
}