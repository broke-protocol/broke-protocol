using System;
using System.Linq;
using BrokeProtocol.Collections;
using BrokeProtocol.Utility.Networking;
using BrokeProtocol.Entities;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Jobs;
using BrokeProtocol.Utility.AI;
using BrokeProtocol.Required;
using BrokeProtocol.Managers;
using BrokeProtocol.Prefabs;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BrokeProtocol.GameSource.Jobs
{
    public class Citizen : Job
    {
        public override void ServerCoroutine()
        {
            if (!player.isHuman && Random.value < 0.005f && player.IsMobile && player.svPlayer.currentState.index == StateIndex.Waypoint)
            {
                player.svPlayer.TryFindInnocent();
            }
        }
    }

    public class Criminal : Job
    {
        public override void ServerCoroutine()
        {
            if (!player.isHuman && Random.value < 0.01f && player.IsMobile && player.svPlayer.currentState.index == StateIndex.Waypoint)
            {
                player.svPlayer.TryFindVictim();
            }
        }
    }

    public class Prisoner : Job
    {
        public override void ResetJobAI() => player.svPlayer.SetState(StateIndex.Waypoint);
    }

    public class Police : Job
    {
        public override void ServerCoroutine()
        {
            if (!player.isHuman && !player.svPlayer.targetEntity && player.IsMobile &&
                player.HasItem(player.manager.handcuffs) &&
                Random.value > player.svPlayer.SaturationLevel(WaypointType.Player, 30f))
            {
                player.svPlayer.TryFindCriminal();
            }
        }

        public override void ResetJobAI()
        {
            if (!SetSpawnTarget())
            {
                base.ResetJobAI();
            }
        }
    }

    public class Paramedic : TargetPlayerJob
    {
        public override void ServerCoroutine()
        {
            if (!player.isHuman)
            {
                if (Random.value < 0.1f && player.IsMobile && !player.svPlayer.targetEntity &&
                    player.HasItem(player.manager.defibrillator))
                {
                    player.svPlayer.TryFindKnockedOut();
                }
            }
            else if (!ValidTarget(target))
            {
                SetTarget();
            }
        }

        protected override void FoundTarget()
        {
            base.FoundTarget();
            player.svPlayer.SendGameMessage(targetPlayer.username + " has been knocked out! Check map");
            targetPlayer.svPlayer.SendGameMessage("Paramedic alerted to your location");
        }

        protected override bool ValidTarget(ShEntity target)
        {
            return base.ValidTarget(target) && (target as ShPlayer).IsKnockedOut;
        }

        public override void OnHealEntity(ShEntity entity)
        {
            player.svPlayer.Reward(2, 100);
        }

        public override void OnRevivePlayer(ShPlayer entity)
        {
            player.svPlayer.Reward(3, 250);
        }
    }

    public class Firefighter : TargetEntityJob
    {
        public override void ServerCoroutine()
        {
            if (!player.isHuman)
            {
                if (Random.value < 0.1f && player.IsMobile && !player.svPlayer.targetEntity &&
                    player.HasItem(player.manager.extinguisher))
                {
                    player.svPlayer.TryFindFire();
                }
            }
            else if (!ValidTarget(target))
            {
                SetTarget();
            }
        }

        protected override GetEntityCallback GetTargetHandler() => () =>
        {
            if (player.svPlayer.svManager.fires.Count > 0)
                return player.svPlayer.svManager.fires.ToArray().GetRandom();
            else
                return null;
        };

        protected override void FoundTarget()
        {
            base.FoundTarget();
            player.svPlayer.SendGameMessage("Fire reported! Check Map");
        }

        public override void OnDamageEntity(ShEntity hitTarget)
        {
            if (hitTarget.svEntity.spawner != player)
            {
                player.svPlayer.Reward(1, 25);
            }
        }
    }

    public class Gangster : Job
    {
        protected int gangstersKilled;

        public override void ServerCoroutine()
        {
            if (!player.isHuman && Random.value < 0.01f && player.IsMobile
                && player.svPlayer.currentState.index == StateIndex.Waypoint)
            {
                player.svPlayer.TryFindEnemyGang();
            }
        }

        public override float GetSpawnRate
        {
            get
            {
                ShTerritory territory = player.svPlayer.GetTerritory;
                if (territory && territory.ownerIndex == info.jobIndex)
                {
                    if (territory.attackerIndex != Util.InvalidByte)
                    {
                        return 1f;
                    }
                    else
                    {
                        return info.spawnRate;
                    }
                }
                return 0f;
            }
        }

        public override void SetJobServer()
        {
            gangstersKilled = 0;
            foreach (ShTerritory territory in player.manager.svManager.territories.Values)
            {
                territory.svEntity.AddSubscribedPlayer(player);
            }
        }

        public override void RemoveJobServer()
        {
            foreach (ShTerritory territory in player.manager.svManager.territories.Values)
            {
                territory.svEntity.RemoveSubscribedPlayer(player, true);
            }
        }

        public override void OnDestroyEntity(ShEntity entity)
        {
            base.OnDestroyEntity(entity);

            if (entity is ShPlayer victim)
            {
                if (!player.svPlayer.svManager.gangWar)
                {
                    if (player.isHuman && victim.job is Gangster && this != victim.job)
                    {
                        ShTerritory t;
                        if (gangstersKilled >= 1 && (t = player.svPlayer.GetTerritory) && t.ownerIndex != info.jobIndex)
                        {
                            t.svTerritory.StartGangWar(info.jobIndex);
                            gangstersKilled = 0;
                        }
                        else
                        {
                            gangstersKilled++;
                        }

                        player.svPlayer.Reward(2, 50);
                    }
                }
                else if (victim.job is Gangster)
                {
                    ShTerritory t = player.svPlayer.GetTerritory;
                    if (t && t.attackerIndex != Util.InvalidByte)
                    {
                        if (victim.job.info.jobIndex == t.ownerIndex)
                        {
                            t.svTerritory.defendersKilled++;
                            t.svTerritory.SendTerritoryStats();
                            player.svPlayer.Reward(3, 100);
                        }
                        else if (victim.job.info.jobIndex == t.attackerIndex)
                        {
                            t.svTerritory.attackersKilled++;
                            t.svTerritory.SendTerritoryStats();
                            player.svPlayer.Reward(3, 100);
                        }
                    }
                }
            }
        }

        public override void ResetJobAI()
        {
            ShPlayer target = player.svPlayer.spawner;

            if (target && target.IsOutside && target.job is Gangster &&
                target.job != this && player.DistanceSqr(target) <= Util.visibleRangeSqr)
            {
                ShTerritory territory = target.svPlayer.GetTerritory;
                if (territory && territory.ownerIndex == info.jobIndex && territory.attackerIndex != Util.InvalidByte)
                {
                    player.svPlayer.targetEntity = target;
                    if (player.svPlayer.SetState(StateIndex.Attack))
                    {
                        return;
                    }
                }
            }
            base.ResetJobAI();
        }
    }

    public class Mayor : Job
    {
        public override void SetJobServer() => player.manager.svManager.mayor = player;

        public override void RemoveJobServer() => player.manager.svManager.mayor = null;
    }

    public abstract class TargetEntityJob : Job
    {
        [NonSerialized]
        public ShEntity target;

        protected delegate ShEntity GetEntityCallback();

        protected virtual bool ValidTarget(ShEntity target) => target && target != player && target.isActiveAndEnabled && !target.IsDead;

        public virtual void ResetTarget()
        {
            player.svPlayer.DestroyGoalMarker();
            target = null;
        }

        protected virtual void FoundTarget() => player.svPlayer.StartGoalMarker(target);

        protected abstract GetEntityCallback GetTargetHandler();

        protected void SetTarget()
        {
            ResetTarget();

            GetEntityCallback handler = GetTargetHandler();

            for (int i = 0; i < 10; i++)
            {
                ShEntity e = handler();

                if (ValidTarget(e))
                {
                    Debug.Log("Found target");
                    target = e;
                    FoundTarget();
                    return;
                }
            }
        }

        public override void RemoveJobServer() => ResetTarget();
    }

    public abstract class TargetPlayerJob : TargetEntityJob
    {
        [NonSerialized]
        public ShPlayer targetPlayer;

        protected override void FoundTarget()
        {
            base.FoundTarget();
            targetPlayer = target as ShPlayer;
        }

        protected override bool ValidTarget(ShEntity target)
        {
            return base.ValidTarget(target) && (target as ShPlayer).wantedLevel >= info.attackLevel;
        }

        protected override GetEntityCallback GetTargetHandler()
        {
            if (EntityCollections.Humans.Count >= 3)
            {
                return () => player.svPlayer.svManager.RandomHuman;
            }
            else
            {
                return () => player.svPlayer.svManager.RandomAIPlayer;
            }
        }

        public override void ResetTarget()
        {
            targetPlayer = null;
            base.ResetTarget();
        }
    }

    public class SpecOps : TargetPlayerJob
    {
        protected override void FoundTarget()
        {
            base.FoundTarget();
            player.svPlayer.SendGameMessage("Assassination target: " + targetPlayer.username);
            targetPlayer.svPlayer.SendGameMessage("SpecOps dispatched!");
        }

        public override void ServerCoroutine()
        {
            if (!player.isHuman)
            {
                if (!player.svPlayer.targetEntity && player.IsMobile &&
                    Random.value > player.svPlayer.SaturationLevel(WaypointType.Player, 30f))
                {
                    player.svPlayer.TryFindCriminal();
                }
            }
            else if (!ValidTarget(targetPlayer))
            {
                SetTarget();
            }
        }

        public override void OnDestroyEntity(ShEntity entity)
        {
            base.OnDestroyEntity(entity);
            if (entity is ShPlayer victim && targetPlayer == victim && victim.wantedLevel > 0 && victim.wantedLevel >= info.attackLevel)
            {
                player.svPlayer.Reward(3, 300);
            }
        }

        public override void ResetJobAI()
        {
            if (!SetSpawnTarget())
            {
                base.ResetJobAI();
            }
        }
    }

    public class DeliveryDriver : TargetPlayerJob
    {
        [NonSerialized]
        public ShConsumable deliveryItem;

        [NonSerialized]
        public float timeDeadline;

        protected override bool ValidTarget(ShEntity target) => base.ValidTarget(target) && (target is ShPlayer p) && !p.curMount && !(p.job is Prisoner);

        public override void ResetTarget()
        {
            if (deliveryItem)
            {
                if (player.HasItem(deliveryItem))
                {
                    player.TransferItem(DeltaInv.RemoveFromMe, deliveryItem);
                }
                deliveryItem = null;
                player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.DestroyTimer);
            }

            base.ResetTarget();
        }

        protected override void FoundTarget()
        {
            base.FoundTarget();
            player.svPlayer.SendGameMessage("Delivery target: " + targetPlayer.username);
            deliveryItem = SceneManager.Instance.consumablesCollection.GetRandom();
            player.TransferItem(DeltaInv.AddToMe, deliveryItem);
            timeDeadline = Time.time + (player.Distance(targetPlayer) * 0.1f) + 20f;
            player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.ShowTimer, timeDeadline - Time.time);
        }

        public override void ServerCoroutine()
        {
            if (player.isHuman)
            {
                if (!ValidTarget(targetPlayer))
                {
                    SetTarget();
                }
                else if (deliveryItem && !player.HasItem(deliveryItem))
                {
                    player.svPlayer.SendGameMessage("Lost Delivery Item");
                    SetTarget();
                }
                else if (targetPlayer && Time.time > timeDeadline)
                {
                    player.svPlayer.SendGameMessage("Out of Time");
                    SetTarget();
                }
            }
        }

        public override void OnSpecialAction(ShPlayer target)
        {
            if (!target || target.IsDead || !(player.job.info.specialAction) || !player.InActionRange(target))
            {
                return;
            }

            if (targetPlayer != target)
            {
                player.svPlayer.SendGameMessage("Wrong target");
                return;
            }

            player.svPlayer.Reward(2, Mathf.CeilToInt(timeDeadline - Time.time));

            target.TransferItem(DeltaInv.AddToMe, deliveryItem);

            player.svPlayer.SendGameMessage("Successfully Delivered " + deliveryItem.itemName + " to " + targetPlayer.username);

            targetPlayer.svPlayer.SendGameMessage("Received delivery from " + player.username);

            ResetTarget();
        }
    }

    public class TaxiDriver : TargetPlayerJob
    {
        [NonSerialized]
        public ShEntity destinationMarker;

        [NonSerialized]
        public float timeDeadline;

        private bool ValidPlayer(ShPlayer player) => player.IsMobile && !(player.job is Prisoner) && player.IsOutside;

        override protected bool ValidTarget(ShEntity target)
        {
            if (target is ShPlayer p)
            {
                if (destinationMarker)
                {
                    return base.ValidTarget(target) && ValidPlayer(p) && p.curMount && p.curMount == player.curMount;
                }
                else
                {
                    return base.ValidTarget(target) && ValidPlayer(p) && !p.curMount;
                }
            }
            return false;
        }

        override public void ResetTarget()
        {
            if (targetPlayer)
            {
                targetPlayer.svPlayer.SvDismount();
                targetPlayer.svPlayer.ResetAI();
            }

            if (destinationMarker)
            {
                destinationMarker.Destroy();
                destinationMarker = null;

                player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.DestroyTimer);
            }

            base.ResetTarget();
        }

        protected override GetEntityCallback GetTargetHandler() => () => player.svPlayer.svManager.RandomAIPlayer;

        public override void ServerCoroutine()
        {
            if (player.isHuman)
            {
                if (!player.IsDriving)
                {
                    ResetTarget();
                }
                else if (!ValidTarget(targetPlayer))
                {
                    SetTarget();
                }
                else if (destinationMarker)
                {
                    if (Time.time > timeDeadline)
                    {
                        player.svPlayer.SendGameMessage("Out of Time");
                        SetTarget();
                    }
                    else if (player.TransportWithinReach(destinationMarker))
                    {
                        player.svPlayer.Reward(2, Mathf.CeilToInt(timeDeadline - Time.time));
                        SetTarget();
                    }
                }
                else if (
                    targetPlayer && player.TransportWithinReach(targetPlayer) &&
                    targetPlayer.svPlayer.SvMountBack(player.curMount.ID, false))
                {
                    player.svPlayer.DestroyGoalMarker();

                    SpawnLocation destination = player.manager.svManager.spawnLocations.GetRandom();

                    destinationMarker = player.manager.svManager.AddNewEntity(
                        player.manager.svManager.markerGoalPrefab,
                        SceneManager.Instance.ExteriorPlace,
                        destination.transform.position,
                        Quaternion.identity,
                        new IDCollection<ShPlayer> { player });

                    player.svPlayer.SendGameMessage("Destination: " + destination.locationName);

                    timeDeadline = Time.time + player.Distance(destination.transform.position) * 0.1f + 20f;

                    player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.ShowTimer, timeDeadline - Time.time);
                }
            }
        }

        protected override void FoundTarget()
        {
            base.FoundTarget();
            player.svPlayer.SendGameMessage("Pickup target: " + targetPlayer.username);
        }
    }
}
