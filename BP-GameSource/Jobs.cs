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
using BrokeProtocol.API;

namespace BrokeProtocol.GameSource.Jobs
{
    public class Citizen : Job
    {
        public override void Loop()
        {
            if (!player.isHuman && Random.value < 0.005f && player.IsMobile && player.svPlayer.currentState.index == StateIndex.Waypoint)
            {
                player.svPlayer.TryFindInnocent();
            }
        }
    }

    public class Hitman : Job
    {
        public override void Loop()
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
        public override void Loop()
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
        public override void Loop()
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
        public override void Loop()
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

        public override void Loop()
        {
            if (!player.isHuman && Random.value < 0.01f && player.IsMobile
                && player.svPlayer.currentState.index == StateIndex.Waypoint)
            {
                player.svPlayer.TryFindEnemyGang();
            }
        }

        public override float GetSpawnRate()
        {
            ShTerritory territory = player.svPlayer.GetTerritory;
            if (territory && territory.ownerIndex == info.shared.jobIndex)
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
                    if (player.isHuman && victim.svPlayer.job is Gangster && this != victim.svPlayer.job)
                    {
                        ShTerritory t;
                        if (gangstersKilled >= 1 && (t = player.svPlayer.GetTerritory) && t.ownerIndex != info.shared.jobIndex)
                        {
                            t.svTerritory.StartGangWar(info.shared.jobIndex);
                            gangstersKilled = 0;
                        }
                        else
                        {
                            gangstersKilled++;
                        }

                        player.svPlayer.Reward(2, 50);
                    }
                }
                else if (victim.svPlayer.job is Gangster)
                {
                    ShTerritory t = player.svPlayer.GetTerritory;
                    if (t && t.attackerIndex != Util.InvalidByte)
                    {
                        if (victim.svPlayer.job.info.shared.jobIndex == t.ownerIndex)
                        {
                            t.svTerritory.defendersKilled++;
                            t.svTerritory.SendTerritoryStats();
                            player.svPlayer.Reward(3, 100);
                        }
                        else if (victim.svPlayer.job.info.shared.jobIndex == t.attackerIndex)
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

            if (target && target.IsOutside && target.svPlayer.job is Gangster &&
                target.svPlayer.job != this && player.DistanceSqr(target) <= Util.visibleRangeSqr)
            {
                ShTerritory territory = target.svPlayer.GetTerritory;
                if (territory && territory.ownerIndex == info.shared.jobIndex && territory.attackerIndex != Util.InvalidByte)
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
        public override void SetJobServer() => ChatHandler.SendToAll("New Mayor: " + player.username);

        public override void RemoveJobServer() => ChatHandler.SendToAll("Mayor Left: " + player.username);


        /*
        requestItems = new RequestItem[requestableItems.Length];
        for (int i = 0; i < requestableItems.Length; i++)
        {
            requestItems[i] = new RequestItem(this, i);
        }
        
        public class ClRequestItemButton : ClButton
        {
            [SerializeField]
            private Text requestItemLabel = null;
            [SerializeField]
            private Text requestPriceLabel = null;

            [NonSerialized]
            public RequestItemInfo requestItemInfo;

            public override void RefreshActions()
            {
                actions.Add(new ActionInfo(ButtonIndex.RequestItemButton, null, RequestAddAction));
                base.RefreshActions();
            }

            public override void Initialize(ButtonInfo buttonInfo)
            {
                base.Initialize(buttonInfo);

                requestItemInfo = (RequestItemInfo)buttonInfo;
                requestItemLabel.text = requestItemInfo.requestItem.item.itemName;
                requestPriceLabel.text = "$" + requestItemInfo.requestItem.item.value.ToString();
            }

            protected void RequestAddAction()
            {
                if (requestItemInfo.manager.clManager.myPlayer.HasItem(requestItemInfo.requestItem.item))
                {
                    requestItemInfo.manager.clManager.ShowGameMessage("Have item already");
                }
                else if (requestItemInfo.manager.clManager.myPlayer.MyMoneyCount < requestItemInfo.requestItem.item.value)
                {
                    requestItemInfo.manager.clManager.ShowGameMessage("Insufficient funds");
                }
                else
                {
                    requestItemInfo.requestItem.RequestAdd();
                }
            }
        }
        

        public class ClRequestButton : ClButton
        {
            [SerializeField]
            private Text itemLabel = null;
            [SerializeField]
            private Text playerLabel = null;
            [SerializeField]
            private Text jobLabel = null;

            [NonSerialized]
            public RequestInfo requestInfo;

            public override void RefreshActions()
            {
                actions.Add(new ActionInfo(ButtonIndex.RequestButton, null, AcceptAction));
                actions.Add(new ActionInfo(ButtonIndex.RequestButton, null, DenyAction));
                base.RefreshActions();
            }

            public override void Initialize(ButtonInfo buttonInfo)
            {
                base.Initialize(buttonInfo);

                requestInfo = (RequestInfo)buttonInfo;
                itemLabel.text = requestInfo.requestItem.item.itemName;
                playerLabel.text = requestInfo.player.username;
                jobLabel.text = requestInfo.player.clPlayer.job.jobName;
            }

            protected void AcceptAction()
            {
                ShPlayer requester = ((RequestInfo)buttonInfo).player;

                buttonInfo.manager.clManager.SendToServer(Channel.Reliable, SvPacket.AcceptRequest, requester.ID);

                buttonInfo.manager.clManager.myPlayer.RequestRemove(requester);
            }

            protected void DenyAction()
            {
                ShPlayer requester = ((RequestInfo)buttonInfo).player;

                buttonInfo.manager.clManager.SendToServer(Channel.Reliable, SvPacket.DenyRequest, requester.ID);

                buttonInfo.manager.clManager.myPlayer.RequestRemove(requester);
            }
        }
        


        public class RequestItemsMenu : ListMenu
        {
            public override void FillButtons(params object[] args)
            {
                foreach (RequestItem requestItem in manager.requestItems)
                {
                    CreateButton(new RequestItemInfo(manager, contentPanel, requestItem));
                }
                base.FillButtons(args);
            }
        }

        public class RequestsMenu : ListMenu
        {
            public override void FillButtons(params object[] args)
            {
                foreach (KeyValuePair<ShPlayer, RequestItem> pair in manager.clManager.myPlayer.requests)
                {
                    CreateButton(new RequestInfo(manager, contentPanel, pair.Key, pair.Value));
                }
                base.FillButtons(args);
            }
        }


        public sealed class RequestItem
        {
            private readonly ShManager manager;
            public int index;
            public ShItem item;

            public RequestItem(ShManager manager, int index)
            {
                this.manager = manager;
                this.index = index;
                item = manager.requestableItems[index];
            }

            public void RequestAdd() => manager.clManager.SendToServer(Channel.Reliable, SvPacket.RequestAdd, index);
        }



        public void RequestAdd(int requestItemIndex)
        {
            if (requestItemIndex >= 0 && requestItemIndex < player.manager.requestableItems.Length)
            {
                RequestItem requestItem = player.manager.requestItems[requestItemIndex];

                if (!player.HasItem(requestItem.item))
                {
                    if (svManager.mayor)
                    {
                        if (player.MyMoneyCount< requestItem.item.value)
                        {
                            SendGameMessage("Not enough money");
                        }
                        else if (svManager.mayor.requests.ContainsKey(player))
                        {
                            SendGameMessage("Previous request still pending");
                        }
                        else
                        {
                            svManager.mayor.RequestAdd(player, requestItem);
                            svManager.mayor.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.RequestAdd, player.ID, requestItemIndex);

                            SendGameMessage("Request successfully sent");
                        }
                    }
                    else
                    {
                        BuyRequestItem(requestItem);
                    }
                }
                else
                {
                    SendGameMessage("Already own item");
                }
            }
        }

        public bool BuyRequestItem(RequestItem requestItem)
        {
            if (player.MyMoneyCount>= requestItem.item.value)
            {
                player.TransferMoney(DeltaInv.RemoveFromMe, requestItem.item.value, true);

                player.TransferItem(DeltaInv.AddToMe, requestItem.item);

                return true;
            }

            return false;
        }

        public void SvAcceptRequest(int playerID)
        {
            ShPlayer requester = EntityCollections.FindByID<ShPlayer>(playerID);
            if (requester)
            {
                var requestItem = player.RequestGet(requester);

                if (requestItem == null)
                {
                    return;
                }

                if (!requester.svPlayer.BuyRequestItem(requestItem))
                {
                    requester.svPlayer.SendGameMessage("No funds for license");
                }
                else
                {
                    player.TransferMoney(DeltaInv.AddToMe, requestItem.item.value, true);
                }

                player.RequestRemove(requester);
            }
        }

        public void SvDenyRequest(int playerID)
        {
            ShPlayer requester = EntityCollections.FindByID<ShPlayer>(playerID);
            if (requester)
            {
                var requestItem = player.RequestGet(requester);

                if (requestItem == null)
                {
                    return;
                }

                requester.svPlayer.SendGameMessage("License Denied");
                player.RequestRemove(requester);
            }
        }

        public RequestItem RequestGet(ShPlayer player)
        {
            requests.TryGetValue(player, out RequestItem requestItem);

            return requestItem;
        }

        public void RequestRemove(ShPlayer player)
        {
            if (requests.ContainsKey(player))
            {
                requests.Remove(player);
                if (ShManager.isClient)
                {
                    manager.clManager.RefreshListMenu<OptionMenu>();
                }
            }
        }

        public void RequestAdd(ShPlayer player, RequestItem requestItem)
        {
            requests.Add(player, requestItem);
            if (ShManager.isClient)
            {
                manager.clManager.ShowGameMessage(player.username + " requesting a " + requestItem.item.itemName);
                manager.clManager.RefreshListMenu<OptionMenu>();
            }
        }

        private void RequestAdd(MyReader reader)
        {
            ShPlayer player = EntityCollections.FindByID<ShPlayer>(reader.ReadInt32());
            if (player)
            {
                RequestItem requestItem = manager.requestItems[reader.ReadInt32()];
                myPlayer.RequestAdd(player, requestItem);
            }
        }

        private void RequestRemove(MyReader reader)
        {
            ShPlayer player = EntityCollections.FindByID<ShPlayer>(reader.ReadInt32());
            if (player) myPlayer.RequestRemove(player);
        }

         */


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

        public override void Loop()
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

    public class DeliveryMan : TargetPlayerJob
    {
        [NonSerialized]
        public ShConsumable deliveryItem;

        [NonSerialized]
        public float timeDeadline;

        protected override bool ValidTarget(ShEntity target) => base.ValidTarget(target) && (target is ShPlayer p) && !p.curMount && !(p.svPlayer.job is Prisoner);

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

        public override void Loop()
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
            if (!target || target.IsDead || !(player.svPlayer.job.info.shared.specialAction) || !player.InActionRange(target))
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

        private bool ValidPlayer(ShPlayer player) => player.IsMobile && !(player.svPlayer.job is Prisoner) && player.IsOutside;

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

        public override void Loop()
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
