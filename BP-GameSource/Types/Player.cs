using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
using BrokeProtocol.Utility.Jobs;
using BrokeProtocol.Utility.Networking;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace BrokeProtocol.GameSource.Types
{
    public class Player : Movable
    {
        [Target(GameSourceEvent.PlayerGlobalChatMessage)]
        protected void OnGlobalChatMessage(ShPlayer player, string message)
        {
            if (player.manager.svManager.chatted.OverLimit(player))
            {
                return;
            }

            message = message.CleanMessage();
            Debug.Log($"[CHAT] {player.username}:{message}");

            if (CommandHandler.OnEvent(player, message)) // 'true' if message starts with command prefix
            {
                return;
            }

            player.manager.svManager.chatted.Add(player);
            player.svPlayer.Send(SvSendType.All, Channel.Unsequenced, ClPacket.GlobalChatMessage, player.ID, message);
        }

        [Target(GameSourceEvent.PlayerLocalChatMessage)]
        protected void OnLocalChatMessage(ShPlayer player, string message)
        {
            if (player.manager.svManager.chatted.OverLimit(player))
            {
                return;
            }

            message = message.CleanMessage();
            Debug.Log($"[CHAT] {player.username}:{message}");

            if (CommandHandler.OnEvent(player, message)) // 'true' if message starts with command prefix
            {
                return;
            }

            player.manager.svManager.chatted.Add(player);
            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Unsequenced, ClPacket.LocalChatMessage, player.ID, message);
        }

        [Target(GameSourceEvent.PlayerDamage)]
        protected void OnDamage(ShPlayer player, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider)
        {
            if (player.IsDead || player.IsShielded(damageIndex, collider))
            {
                return;
            }

            if (player.IsBlocking(damageIndex))
            {
                amount *= 0.3f;
            }
            else if (collider == player.headCollider) // Headshot
            {
                amount *= 2f;
            }

            if (!player.isHuman)
            {
                amount /= player.svPlayer.svManager.settings.difficulty;
            }

            amount -= amount * (player.armorLevel / player.maxStat * 0.5f);

            base.OnDamage(player, damageIndex, amount, attacker, collider);

            if (player.IsDead)
            {
                return;
            }

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
                if (player.wantedLevel == 0)
                {
                    if (attacker.curEquipable is ShGun)
                    {
                        attacker.svPlayer.SvAddCrime(CrimeIndex.ArmedAssault, player);
                    }
                    else
                    {
                        attacker.svPlayer.SvAddCrime(CrimeIndex.Assault, player);
                    }
                }

                if (!player.isHuman)
                {
                    player.svPlayer.targetEntity = attacker;
                    player.svPlayer.SetState(StateIndex.Attack);
                }
            }
        }

        [Target(GameSourceEvent.PlayerDeath)]
        protected void OnDeath(ShPlayer player)
        {
            if (player.svPlayer.lastAttacker && player.svPlayer.lastAttacker != player)
            {
                player.svPlayer.lastAttacker.job.OnKillPlayer(player);

                // Only drop items if attacker present, to prevent AI suicide item farming
                if (Physics.Raycast(
                    player.GetPosition + Vector3.up,
                    Vector3.down,
                    out RaycastHit hit,
                    10f,
                    MaskIndex.defaultMask))
                {
                    ShEntity briefcase = player.manager.svManager.AddNewEntity(
                        player.manager.svManager.briefcasePrefabs.GetRandom(),
                        player.GetPlace,
                        hit.point,
                        Quaternion.LookRotation(player.GetPositionT.forward, Vector3.up),
                        false);

                    if (briefcase)
                    {
                        foreach (KeyValuePair<int, InventoryItem> pair in player.myItems)
                        {
                            if (Random.value < 0.8f)
                            {
                                InventoryItem i = new InventoryItem(
                                    pair.Value.item,
                                    Mathf.CeilToInt(pair.Value.count * Random.Range(0.05f, 0.3f)));
                                briefcase.myItems.Add(pair.Key, i);
                            }
                        }
                    }
                }
            }

            player.RemoveItemsDeath();

            player.svPlayer.ClearWitnessed();

            player.DeactivateEffects();

            if (!player.isHuman)
            {
                player.svPlayer.SetState(StateIndex.Null);
            }

            player.svPlayer.Send(SvSendType.Self,Channel.Reliable, ClPacket.ShowTimer, player.svPlayer.RespawnTime);

            player.SetStance(StanceIndex.Dead, true);
        }

        [Target(GameSourceEvent.PlayerBuyApartment)]
        protected void OnBuyApartment(ShPlayer player, ShApartment apartment)
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

        [Target(GameSourceEvent.PlayerSellApartment)]
        protected void OnSellApartment(ShPlayer player, ShApartment apartment)
        {
            if (!player.manager.svManager.trySell.OverLimit(player))
            {
                player.manager.svManager.trySell.Add(player);
                player.svPlayer.SendGameMessage("Are you sure? Sell again to confirm..");
                return;
            }

            if (player.ownedApartments.TryGetValue(apartment, out Place place))
            {
                if (player.GetPlace == place)
                {
                    player.svPlayer.SvEnterDoor(place.mainDoor.ID, player, true);
                }

                player.TransferMoney(DeltaInv.AddToMe, apartment.value / 2, true);

                player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.SellApartment, apartment.ID);
                player.svPlayer.CleanupApartment(place);

                return;
            }
        }

        [Target(GameSourceEvent.PlayerInvite)]
        protected void OnInvite(ShPlayer player, ShPlayer other)
        {
            if (other.isHuman && !other.IsDead && other.IsUp && other.IsOutside)
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

        [Target(GameSourceEvent.PlayerKickOut)]
        protected void OnKickOut(ShPlayer player, ShPlayer other)
        {
            if (other.isHuman && !other.IsDead && other.IsUp && player.InOwnApartment && other.GetPlace == player.GetPlace)
            {
                other.svPlayer.SvEnterDoor(other.GetPlace.mainDoor.ID, player, true);
            }
        }

        [Target(GameSourceEvent.PlayerRespawn)]
        protected void OnRespawn(ShPlayer player)
        {
            if (player.isHuman)
            {
                var newSpawn = player.svPlayer.svManager.spawnLocations.GetRandom().transform;
                player.originalPosition = newSpawn.position;
                player.originalRotation = newSpawn.rotation;
                player.originalParent = newSpawn.parent;
            }

            base.OnRespawn(player);
        }

        [Target(GameSourceEvent.PlayerReward)]
        protected void OnReward(ShPlayer player, int experienceDelta, int moneyDelta)
        {
            if (!player.isHuman || player.job.info.rankItems.Length <= 1)
            {
                return;
            }

            var experience = player.experience + experienceDelta;

            if (experience > Util.maxExperience)
            {
                if (player.rank >= player.job.info.rankItems.Length - 1)
                {
                    if (player.experience != Util.maxExperience)
                    {
                        player.svPlayer.SetExperience(Util.maxExperience, true);
                    }
                }
                else
                {
                    int newRank = player.rank + 1;
                    player.svPlayer.AddJobItems(player.job, player.rank, newRank, false);
                    player.svPlayer.SetRank(newRank);
                    player.svPlayer.SetExperience(experience - Util.maxExperience, false);
                }
            }
            else if (experience <= 0)
            {
                if (player.rank <= 0)
                {
                    player.svPlayer.Send(SvSendType.Self, Channel.Unsequenced, ClPacket.GameMessage, "You lost your job");
                    player.svPlayer.SvResetJob(false);
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
                player.TransferMoney(DeltaInv.AddToMe, moneyDelta, true);
            }
            else if (moneyDelta < 0)
            {
                player.TransferMoney(DeltaInv.RemoveFromMe, -moneyDelta, true);
            }
        }

        [Target(GameSourceEvent.PlayerAcceptRequest)]
        protected void OnAcceptRequest(ShPlayer player, ShPlayer requester)
        {
            var requestItem = player.RequestGet(requester);

            if (requestItem == null)
            {
                return;
            }

            if (!requester.svPlayer.BuyRequestItem(requestItem))
            {
                requester.svPlayer.Send(SvSendType.Self, Channel.Unsequenced, ClPacket.GameMessage, "No funds for license");
            }
            else
            {
                player.TransferMoney(DeltaInv.AddToMe, requestItem.item.value, true);
            }

            player.RequestRemove(requester);
        }

        [Target(GameSourceEvent.PlayerDenyRequest)]
        protected void OnDenyRequest(ShPlayer player, ShPlayer requester)
        {
            var requestItem = player.RequestGet(requester);

            if (requestItem == null)
            {
                return;
            }

            requester.svPlayer.Send(SvSendType.Self, Channel.Unsequenced, ClPacket.GameMessage, "License Denied");
            player.RequestRemove(requester);
        }

        [Target(GameSourceEvent.PlayerCrime)]
        protected void OnCrime(ShPlayer player, byte crimeIndex, ShPlayer victim)
        {
            if (player.svPlayer.InvalidCrime(crimeIndex))
            {
                return;
            }

            Crime crime = player.manager.GetCrime(crimeIndex);
            ShPlayer witness = null;
            if (crime.witness && !player.svPlayer.GetWitness(victim, out witness))
            {
                return;
            }

            player.AddCrime(crime.index, witness);
            player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.AddCrime, crime.index, witness ? witness.ID : 0);

            if (player.job.info.groupIndex != GroupIndex.Criminal)
            {
                player.svPlayer.Reward(-crime.experiencePenalty, -crime.fine);
            }
        }

        [Target(GameSourceEvent.PlayerKick)]
        protected void OnKick(Managers.SvManager svManager, ShPlayer target, string reason)
        {
            svManager.SendToAll(Channel.Unsequenced, ClPacket.GameMessage, $"{target.fullname} Kicked: {reason}");

            svManager.KickConnection(target.svPlayer.connection);
        }

        [Target(GameSourceEvent.PlayerBan)]
        protected void OnBan(Managers.SvManager svManager, ShPlayer target, string reason)
        {
            svManager.SendToAll(Channel.Unsequenced, ClPacket.GameMessage, $"{target.fullname} Banned: {reason}");

            target.svPlayer.PlayerData.Ban(reason);
            svManager.Disconnect(target.svPlayer.connection, DisconnectTypes.Banned);
        }

        [Target(GameSourceEvent.PlayerRemoveItemsDeath)]
        protected void OnRemoveItemsDeath(ShPlayer player)
        {
            // Allows players to keep items/rewards from job ranks
            foreach (InventoryItem myItem in player.myItems.Values.ToArray())
            {
                int extra = myItem.count;

                if (player.job.info.rankItems.Length > player.rank)
                {
                    for (int rankIndex = player.rank; rankIndex >= 0; rankIndex--)
                    {
                        foreach (InventoryItem i in player.job.info.rankItems[rankIndex].items)
                        {
                            if (myItem.item.index == i.item.index)
                            {
                                extra = Mathf.Max(0, myItem.count - i.count);
                            }
                        }
                    }
                }

                // Remove everything except legal items currently worn
                if (extra > 0 && (myItem.item.illegal || !(myItem.item is ShWearable w) || player.curWearables[(int)w.type].index != w.index))
                {
                    player.TransferItem(DeltaInv.RemoveFromMe, myItem.item.index, extra, true);
                }
            }
        }

        [Target(GameSourceEvent.PlayerRemoveItemsJail)]
        protected void OnRemoveItemsJail(ShPlayer player)
        {
            foreach (InventoryItem i in player.myItems.Values.ToArray())
            {
                if (i.item.illegal)
                {
                    player.TransferItem(DeltaInv.RemoveFromMe, i.item.index, i.count, true);
                }
            }
        }
    }
}
