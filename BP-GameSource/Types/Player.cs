using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
using BrokeProtocol.Utility.Jobs;
using BrokeProtocol.Utility.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BrokeProtocol.GameSource.Types
{
    public class Player : Movable
    {
        //[Target(GameSourceEvent.PlayerInitialize, ExecutionMode.Override)]
        //public void OnInitialize(ShPlayer player) { }

        //[Target(GameSourceEvent.PlayerDestroy, ExecutionMode.Override)]
        //public void OnDestroy(ShPlayer player) { }

        //[Target(GameSourceEvent.PlayerAddItem, ExecutionMode.Override)]
        //public void OnAddItem(ShPlayer player, int itemIndex, int amount, bool dispatch) { }

        //[Target(GameSourceEvent.PlayerRemoveItem, ExecutionMode.Override)]
        //public void OnRemoveItem(ShPlayer player, int itemIndex, int amount, bool dispatch) { }

        //[Target(GameSourceEvent.PlayerCommand, ExecutionMode.Override)]
        //public void OnCommand(ShPlayer player, string message) { }

        //[Target(GameSourceEvent.PlayerSave, ExecutionMode.Override)]
        //public void OnSave(ShPlayer player) { }

        [Target(GameSourceEvent.PlayerGlobalChatMessage, ExecutionMode.Override)]
        public void OnGlobalChatMessage(ShPlayer player, string message)
        {
            if (player.manager.svManager.chatted.Limit(player)) return;

            message = message.CleanMessage();

            if(string.IsNullOrWhiteSpace(message)) return;

            Debug.Log($"[CHAT] {player.username}:{message}");

            // 'true' if message starts with command prefix
            if (CommandHandler.OnEvent(player, message)) return;

            player.svPlayer.Send(SvSendType.All, Channel.Reliable, ClPacket.GlobalChatMessage, player.ID, message);
        }

        [Target(GameSourceEvent.PlayerLocalChatMessage, ExecutionMode.Override)]
        public void OnLocalChatMessage(ShPlayer player, string message)
        {
            if (player.manager.svManager.chatted.Limit(player)) return;

            message = message.CleanMessage();

            if (string.IsNullOrWhiteSpace(message)) return;

            Debug.Log($"[CHAT] {player.username}:{message}");

            // 'true' if message starts with command prefix
            if (CommandHandler.OnEvent(player, message)) return;

            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.LocalChatMessage, player.ID, message);
        }

        [Target(GameSourceEvent.PlayerDamage, ExecutionMode.Override)]
        public void OnDamage(ShPlayer player, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, float hitY)
        {
            if (player.svPlayer.godMode || player.IsDead || player.IsShielded(damageIndex, collider)) return;

            if (damageIndex != DamageIndex.Null)
            {
                BodyEffect effect;
                float random = Random.value;

                if (random < 0.6f)
                    effect = BodyEffect.Null;
                else if (random < 0.8f)
                    effect = BodyEffect.Pain;
                else if (random < 0.925f)
                    effect = BodyEffect.Bloodloss;
                else
                    effect = BodyEffect.Fracture;

                BodyPart part;

                if(damageIndex == DamageIndex.Random)
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
                else if (hitY >= player.capsule.height * 0.75f)
                {
                    part = Random.value < 0.5f ? BodyPart.Arms : BodyPart.Chest;
                }
                else if (hitY >= player.capsule.height * 0.5f)
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
                amount /= player.svPlayer.svManager.settings.difficulty;
            }

            amount -= amount * (player.armorLevel / player.maxStat * 0.5f);

            base.OnDamage(player, damageIndex, amount, attacker, collider, hitY);

            if (player.IsDead) return;

            // Still alive, do knockdown and Assault crimes

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
                    player.svPlayer.SetAttackState(attacker);
                }
                else
                {
                    ShPlayer playerFollower = player.svPlayer.follower;
                    if (playerFollower) playerFollower.svPlayer.SetAttackState(attacker);
                }

                ShPlayer attackerFollower = attacker.svPlayer.follower;

                if (attackerFollower)
                {
                    attackerFollower.svPlayer.SetAttackState(player);
                }
            }
        }

        private IEnumerator SpectateDelay(ShPlayer player, ShPlayer target)
        {
            yield return new WaitForSeconds(2f);
            player.svPlayer.SvSpectate(target);

        }

        [Target(GameSourceEvent.PlayerDeath, ExecutionMode.Override)]
        public void OnDeath(ShPlayer player, ShPlayer attacker)
        {
            if (attacker && attacker != player)
            {
                if (player.isHuman) player.StartCoroutine(SpectateDelay(player, attacker));

                player.svPlayer.RemoveItemsDeath(true);
            }
            else
            {
                player.svPlayer.RemoveItemsDeath(false);
            }

            player.svPlayer.ClearWitnessed();

            if (!player.isHuman) player.svPlayer.SetState(StateIndex.Null);

            player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.ShowTimer, player.svPlayer.RespawnTime);

            player.SetStance(StanceIndex.Dead);
        }

        private const string securityPanel = "securityPanel";
        private const string enterPasscode = "enterPasscode";
        private const string setPasscode = "setPasscode";
        private const string clearPasscode = "clearPasscode";

        [Target(GameSourceEvent.PlayerSecurityPanel, ExecutionMode.Override)]
        public void OnSecurityPanel(ShPlayer player, ShApartment apartment)
        {
            List<LabelID> options = new List<LabelID>();

            options.Add(new LabelID("Enter Passcode", enterPasscode));
            options.Add(new LabelID("Set Passcode", setPasscode));
            options.Add(new LabelID("Clear Passcode", clearPasscode));
            player.svPlayer.SendOptionMenu("&7Security Panel", apartment.ID, securityPanel, options.ToArray(), new LabelID[] { new LabelID("Select", string.Empty) });
        }

        [Target(GameSourceEvent.PlayerBuyApartment, ExecutionMode.Override)]
        public void OnBuyApartment(ShPlayer player, ShApartment apartment)
        {
            if (player.ownedApartments.ContainsKey(apartment))
            {
                player.svPlayer.SendGameMessage("Already owned");
            }
            else if (apartment.svApartment.BuyEntity(player))
            {
                apartment.svApartment.SvSetApartmentOwner(player);
            }
        }

        [Target(GameSourceEvent.PlayerSellApartment, ExecutionMode.Override)]
        public void OnSellApartment(ShPlayer player, ShApartment apartment)
        {
            if (!player.manager.svManager.trySell.Limit(player))
            {
                player.svPlayer.SendGameMessage("Are you sure? Sell again to confirm..");
                return;
            }

            if (player.ownedApartments.TryGetValue(apartment, out var place))
            {
                if (player.GetPlace == place)
                {
                    player.svPlayer.SvEnterDoor(place.mainDoor.ID, player, true);
                }

                player.TransferMoney(DeltaInv.AddToMe, apartment.value / 2);

                player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.SellApartment, apartment.ID);
                player.svPlayer.CleanupApartment(place);
            }
        }

        [Target(GameSourceEvent.PlayerInvite, ExecutionMode.Override)]
        public void OnInvite(ShPlayer player, ShPlayer other)
        {
            if (other.isHuman && other.IsUp && player.IsMobile && !player.InOwnApartment)
            {
                foreach (ShApartment apartment in player.ownedApartments.Keys)
                {
                    if (apartment.DistanceSqr(other) <= Util.inviteDistanceSqr)
                    {
                        other.svPlayer.SvEnterDoor(apartment.ID, player, true);
                        return;
                    }
                }
            }
        }

        [Target(GameSourceEvent.PlayerKickOut, ExecutionMode.Override)]
        public void OnKickOut(ShPlayer player, ShPlayer other)
        {
            if (other.isHuman && other.IsUp && player.IsMobile && player.InOwnApartment && other.GetPlace == player.GetPlace)
            {
                other.svPlayer.SvEnterDoor(other.GetPlace.mainDoor.ID, player, true);
            }
        }

        [Target(GameSourceEvent.PlayerRespawn, ExecutionMode.Override)]
        public void OnRespawn(ShPlayer player)
        {
            if (player.isHuman)
            {
                var newSpawn = player.svPlayer.svManager.spawnLocations.GetRandom().transform;
                player.svPlayer.originalPosition = newSpawn.position;
                player.svPlayer.originalRotation = newSpawn.rotation;
                player.svPlayer.originalParent = newSpawn.parent;
            }

            base.OnRespawn(player);

            if(player.isHuman)
            {
                // Back to spectate self on Respawn
                player.svPlayer.SvSpectate(player);
            }

            player.svPlayer.SvForceEquipable(player.manager.hands.index);
        }

        [Target(GameSourceEvent.PlayerReward, ExecutionMode.Override)]
        public void OnReward(ShPlayer player, int experienceDelta, int moneyDelta)
        {
            if (!player.isHuman || player.svPlayer.job.info.upgrades.Length <= 1) return;

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
                    int newRank = player.rank + 1;
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

            moneyDelta *= player.svPlayer.svManager.payScale[player.rank];

            if (moneyDelta > 0)
            {
                player.TransferMoney(DeltaInv.AddToMe, moneyDelta);
            }
            else if (moneyDelta < 0)
            {
                player.TransferMoney(DeltaInv.RemoveFromMe, -moneyDelta);
            }
        }

        [Target(GameSourceEvent.PlayerJailCriminal, ExecutionMode.Override)]
        public void OnJailCriminal(ShPlayer player, ShPlayer criminal)
        {
            if (player.svPlayer.job.info.shared.groupIndex == GroupIndex.LawEnforcement)
            {
                int fine = criminal.svPlayer.SvGoToJail();

                if (fine > 0) player.svPlayer.job.OnJailCriminal(criminal, fine);
                else player.svPlayer.SendGameMessage("Confirm criminal is cuffed and has crimes");
            }
        }

        [Target(GameSourceEvent.PlayerGoToJail, ExecutionMode.Override)]
        public void OnGoToJail(ShPlayer player, float time, int fine)
        {
            player.svPlayer.SvTrySetJob(BPAPI.Instance.PrisonerIndex, true, false);
            Transform jailSpawn = SceneManager.Instance.jail.mainT;
            player.svPlayer.SvRestore(jailSpawn.position, jailSpawn.rotation, jailSpawn.parent.GetSiblingIndex());
            player.svPlayer.SvForceEquipable(player.manager.hands.index);
            player.svPlayer.SvClearCrimes();
            player.svPlayer.RemoveItemsJail();
            player.svPlayer.StartJailTimer(time);
        }

        [Target(GameSourceEvent.PlayerCrime, ExecutionMode.Override)]
        public void OnCrime(ShPlayer player, byte crimeIndex, ShPlayer victim)
        {
            if (player.svPlayer.godMode || player.svPlayer.InvalidCrime(crimeIndex)) return;

            Crime crime = player.manager.GetCrime(crimeIndex);
            ShPlayer witness = null;

            if (crime.witness && !player.svPlayer.GetWitness(victim, out witness)) return;

            player.AddCrime(crime.index, witness);
            player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.AddCrime, crime.index, witness ? witness.ID : 0);

            if (!player.svPlayer.job.IsCriminal)
            {
                player.svPlayer.Reward(-crime.experiencePenalty, -crime.fine);
            }
        }

        [Target(GameSourceEvent.PlayerInjury, ExecutionMode.Override)]
        public void OnInjury(ShPlayer player, BodyPart part, BodyEffect effect, byte amount)
        {
            player.AddInjury(part, effect, amount);
            player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.AddInjury, (byte)part, (byte)effect, amount);
        }

        [Target(GameSourceEvent.PlayerKick, ExecutionMode.Override)]
        public void OnKick(ShPlayer player, ShPlayer target, string reason)
        {
            ChatHandler.SendToAll($"{target.displayName} Kicked: {reason}");

            player.manager.svManager.KickConnection(target.svPlayer.connection);
        }

        [Target(GameSourceEvent.PlayerBan, ExecutionMode.Override)]
        public void OnBan(ShPlayer player, ShPlayer target, string reason)
        {
            ChatHandler.SendToAll($"{target.displayName} Banned: {reason}");

            player.svPlayer.SvBanDatabase(target.username, reason);
            player.manager.svManager.Disconnect(target.svPlayer.connection, DisconnectTypes.Banned);
        }

        [Target(GameSourceEvent.PlayerRemoveItemsDeath, ExecutionMode.Override)]
        public void OnRemoveItemsDeath(ShPlayer player, bool dropItems)
        {
            List<InventoryItem> removedItems = new List<InventoryItem>();

            // Allows players to keep items/rewards from job ranks
            foreach (InventoryItem myItem in player.myItems.Values.ToArray())
            {
                int extra = myItem.count;

                if (player.svPlayer.job.info.upgrades.Length > player.rank)
                {
                    for (int rankIndex = player.rank; rankIndex >= 0; rankIndex--)
                    {
                        foreach (InventoryStruct i in player.svPlayer.job.info.upgrades[rankIndex].items)
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

            if (dropItems)
            {
                // Only drop items if attacker present, to prevent AI suicide item farming
                if (Physics.Raycast(
                    player.GetPosition + Vector3.up,
                    Vector3.down,
                    out RaycastHit hit,
                    10f,
                    MaskIndex.world))
                {
                    ShEntity briefcase = player.manager.svManager.AddNewEntity(
                        player.manager.svManager.briefcasePrefabs.GetRandom(),
                        player.GetPlace,
                        hit.point,
                        Quaternion.LookRotation(player.GetPositionT.forward),
                        false);

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
            }
        }

        [Target(GameSourceEvent.PlayerRemoveItemsJail, ExecutionMode.Override)]
        public void OnRemoveItemsJail(ShPlayer player)
        {
            foreach (InventoryItem i in player.myItems.Values.ToArray())
            {
                if (i.item.illegal)
                {
                    player.TransferItem(DeltaInv.RemoveFromMe, i.item.index, i.count, true);
                }
            }
        }


        [Target(GameSourceEvent.PlayerRestrain, ExecutionMode.Override)]
        public void OnRestrain(ShPlayer player, ShRestrained restrained)
        {
            if(player.svPlayer.godMode) return;

            if (player.curMount) player.svPlayer.SvDismount();

            player.svPlayer.SvSetEquipable(restrained.index);

            if (!player.isHuman)
            {
                player.svPlayer.SetState(StateIndex.Restrained);
            }
            else
            {
                player.svPlayer.SendGameMessage("You've been restrained");
            }
        }

        [Target(GameSourceEvent.PlayerUnrestrain, ExecutionMode.Override)]
        public void OnUnrestrain(ShPlayer player)
        {
            player.svPlayer.SvSetEquipable(player.manager.hands.index);

            if (!player.isHuman)
            {
                player.svPlayer.SvDismount();
                player.svPlayer.ResetAI();
            }
            else
            {
                player.svPlayer.SendGameMessage("You've been freed");
            }
        }

        [Target(GameSourceEvent.PlayerServerInfo, ExecutionMode.Override)]
        public void OnServerInfo(ShPlayer player)
        {
            player.svPlayer.SendTextMenu("&7Server Info", player.svPlayer.svManager.serverDescription);
        }

        [Target(GameSourceEvent.PlayerDisplayName, ExecutionMode.Override)]
        public void OnDisplayName(ShPlayer player, string username)
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
        }

        [Target(GameSourceEvent.PlayerEnterDoor, ExecutionMode.Override)]
        public void OnEnterDoor(ShPlayer player, ShDoor door, ShPlayer sender, bool forceEnter)
        {
            if (!forceEnter)
            {
                if (player.IsRestrained)
                {
                    player.svPlayer.SendGameMessage("You are restrained");
                    return;
                }

                if (door.svDoor.key && !player.HasItem(door.svDoor.key))
                {
                    player.svPlayer.SendGameMessage("Need " + door.svDoor.key.itemName + " to enter");
                    return;
                }
            }

            ShMountable baseEntity;

            if (player.curMount is ShPlayer mountPlayer)
            {
                baseEntity = mountPlayer;
            }
            else
            {
                baseEntity = player;
                if (player.curMount) player.svPlayer.SvDismount();
            }

            if (door is ShApartment apartment && sender.ownedApartments.TryGetValue(apartment, out var place))
            {
                baseEntity.svMountable.SvRelocate(place.mainDoor.spawnPoint, place.mTransform);
            }
            else
            {
                ShDoor otherDoor = door.svDoor.other;
                baseEntity.svMountable.SvRelocate(otherDoor.spawnPoint, otherDoor.GetPlace.mTransform);
            }
        }


        [Target(GameSourceEvent.PlayerFollower, ExecutionMode.Override)]
        public void OnFollower(ShPlayer player, ShPlayer other)
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
            else if (!other.svPlayer.leader && other.CanFollow && !other.svPlayer.IsBusy)
            {
                other.svPlayer.SetFollowState(player);
            }
            else
            {
                player.svPlayer.SendGameMessage("NPC is occupied");
            }
        }

        [Target(GameSourceEvent.PlayerOptionAction, ExecutionMode.Override)]
        public void OnOptionAction(ShPlayer player, int targetID, string menuID, string optionID, string actionID)
        {
            if(menuID == securityPanel)
            {
                switch (optionID)
                {
                    case enterPasscode:
                        player.svPlayer.SendInputMenu("Enter Passcode", targetID, enterPasscode, InputField.ContentType.Password);
                        break;
                    case setPasscode:
                        player.svPlayer.SendInputMenu("Set Passcode", targetID, setPasscode, InputField.ContentType.Password);
                        break;
                    case clearPasscode:
                        var apartment = EntityCollections.FindByID<ShApartment>(targetID);
                        if (apartment && player.ownedApartments.TryGetValue(apartment, out var apartmentPlace))
                        {
                            apartmentPlace.svPasscode = null;
                            player.svPlayer.SendGameMessage("Apartment Passcode Cleared");
                            return;
                        }
                        player.svPlayer.SendGameMessage("No Apartment Owned");
                        break;
                }

                return;
            }

            if (targetID >= 0)
            {
                player.svPlayer.job.OnOptionMenuAction(targetID, menuID, optionID, actionID);
                return;
            }

            ShPlayer target = EntityCollections.FindByID<ShPlayer>(-targetID);

            if (target)
            {
                target.svPlayer.job.OnOptionMenuAction(player.ID, menuID, optionID, actionID);
            }
        }

        [Target(GameSourceEvent.PlayerSubmitInput, ExecutionMode.Override)]
        public void OnSubmitInput(ShPlayer player, int targetID, string menuID, string input)
        {
            switch (menuID)
            {
                case enterPasscode:
                    var a1 = EntityCollections.FindByID<ShApartment>(targetID);

                    foreach(var a in a1.svApartment.clones.Values)
                    {
                        if(a.svPasscode != null && a.svPasscode == input)
                        {
                            player.svPlayer.SvEnterDoor(targetID, a.svOwner, true);
                            return;
                        }
                    }
                    player.svPlayer.SendGameMessage("Passcode: No Match");
                    break;

                case setPasscode:
                    var a2 = EntityCollections.FindByID<ShApartment>(targetID);
                    if (a2 && player.ownedApartments.TryGetValue(a2, out var ap2))
                    {
                        ap2.svPasscode = input;
                        player.svPlayer.SendGameMessage("Apartment Passcode Set");
                        return;
                    }
                    player.svPlayer.SendGameMessage("No Apartment Owned");
                    break;
            }
        }

        [Target(GameSourceEvent.PlayerReady, ExecutionMode.Override)]
        public void OnReady(ShPlayer player)
        {
            player.svPlayer.SendServerInfo();
        }

        [Target(GameSourceEvent.PlayerPoint, ExecutionMode.Override)]
        public void OnPoint(ShPlayer player, bool pointing)
        {
            player.pointing = pointing;
            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.Point, player.ID, pointing);

            if(pointing && player.svPlayer.follower && 
                Physics.Raycast(player.GetOrigin, player.GetRotationT.forward, out var hit, Util.visibleRange, MaskIndex.hard) && 
                player.svPlayer.follower.svPlayer.NodeNear(hit.point))
            {
                player.svPlayer.follower.svPlayer.SetGoToState(hit.point, Quaternion.LookRotation(hit.point - player.svPlayer.follower.GetPosition), player.GetParent);
            }
        }

        [Target(GameSourceEvent.PlayerAlert, ExecutionMode.Override)]
        public void OnAlert(ShPlayer player)
        {
            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.Alert, player.ID);
            if(player.svPlayer.follower)
            {
                player.svPlayer.follower.svPlayer.SetFollowState(player);
            }
        }
    }
}
