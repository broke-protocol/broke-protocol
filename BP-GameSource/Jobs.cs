using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Prefabs;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
using BrokeProtocol.Utility.Jobs;
using BrokeProtocol.Utility.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BrokeProtocol.GameSource.Jobs
{
    public class LoopJob : Job
    {
        public override void SetJob()
        {
            base.SetJob();
            if (player.isActiveAndEnabled)
            {
                if (player.jobCoroutine != null) player.StopCoroutine(player.jobCoroutine);
                player.jobCoroutine = player.StartCoroutine(JobCoroutine());
            }
        }

        private IEnumerator JobCoroutine()
        {
            WaitForSeconds delay = new WaitForSeconds(1f);
            do
            {
                yield return delay;
                Loop();
            } while (true);
        }

        public virtual void Loop() { }
    }


    public class Citizen : LoopJob
    {
        protected void TryFindInnocent()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => (e is ShPlayer p) && !p.curMount && !p.IsDead && p.IsRestrained && p.wantedLevel == 0 && player.CanSeeEntity(e),
                (e) =>
                {
                    player.svPlayer.targetEntity = e;
                    player.svPlayer.SetState(StateIndex.Free);
                });
        }

        public void TryFindVictim()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && !p.curMount && !p.IsDead && !p.IsRestrained && player.CanSeeEntity(e),
                (e) =>
                {
                    player.svPlayer.targetEntity = e;
                    player.svPlayer.SetState(StateIndex.Rob);
                });
        }

        public override void Loop()
        {
            if(!player.isHuman && !player.svPlayer.targetEntity && !player.IsDead && player.IsMobile && player.svPlayer.currentState.index == StateIndex.Waypoint)
            {
                float rand = Random.value;

                if (rand < 0.01f) TryFindInnocent();
                else if(rand < 0.005f) TryFindVictim();
            }
        }
    }

    public class Hitman : LoopJob
    {
        public const string bountiesKey = "bounties";
        public static readonly Dictionary<string, DateTimeOffset> bounties = new Dictionary<string, DateTimeOffset>();
        public static ShPlayer aiTarget;

        private const string playersMenu = "players";
        private const string bountiesMenu = "bounties";

        private const string place = "place";
        private const string cancel = "cancel";

        private const int placeCost = 2000;
        private const int cancelCost = 3000;

        private const float bountyLimitHours = 100f;

        protected void TryFindBounty()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && (p.svPlayer.job is SpecOps || bounties.ContainsKey(p.username)) && player.CanSeeEntity(e),
                (e) =>
                {
                    // Add double murder to ensure high wanted level (targetable by SpecOps)
                    player.AddCrime(CrimeIndex.Murder, e as ShPlayer);
                    player.AddCrime(CrimeIndex.Murder, e as ShPlayer);
                    player.svPlayer.SetAttackState(e);
                });
        }

        public override void OnDestroyEntity(ShEntity entity)
        {
            base.OnDestroyEntity(entity);
            if (entity is ShPlayer victim && bounties.ContainsKey(victim.username))
            {
                player.svPlayer.Reward(3, 1000);
                bounties.Remove(victim.username);
                ChatHandler.SendToAll($"{player.username} assassinated {victim.username}");

                if (victim == aiTarget) aiTarget = null;
            }
        }

        public override void Loop()
        {
            List<string> removeKeys = new List<string>();

            foreach (KeyValuePair<string, DateTimeOffset> pair in bounties)
            {
                if ((Util.CurrentTime - pair.Value).Hours >= bountyLimitHours)
                {
                    removeKeys.Add(pair.Key);
                }
            }

            foreach (string s in removeKeys) bounties.Remove(s);

            if (player.IsDead) return;

            if (!player.isHuman)
            {
                if (!player.svPlayer.targetEntity && Random.value < 0.1f && player.IsMobile && player.svPlayer.currentState.index == StateIndex.Waypoint)
                {
                    TryFindBounty();
                }
            }
            else if(!aiTarget)
            {
                aiTarget = EntityCollections.RandomNPC;

                if (aiTarget) AddBounty(aiTarget.username);
            }
        }

        public override void OnEmployeeAction(ShPlayer target, string actionID)
        {
            List<LabelID> options = new List<LabelID>();

            foreach (ShPlayer p in EntityCollections.Humans)
            {
                if (bounties.TryGetValue(p.username, out var bountyTime))
                {
                    options.Add(new LabelID($"{p.username}: {bountyLimitHours - (Util.CurrentTime - bountyTime).Hours} Hours", p.username));
                }
                else
                {
                    options.Add(new LabelID($"{p.username} &aAvailable", p.username));
                }    
            }

            // Negative playerID means job action is called on the employer with that ID, not self
            target.svPlayer.SendOptionMenu("&6Players", -player.ID, playersMenu, options.ToArray(), new LabelID[] { new LabelID($"Place Bounty ${placeCost}", place), new LabelID($"Cancel Bounty ${cancelCost}", cancel) });
        }

        public override void OnSelfAction(string actionID)
        {
            List<LabelID> options = new List<LabelID>();

            foreach (KeyValuePair<string, DateTimeOffset> pair in bounties)
            {
                string online = EntityCollections.Accounts.ContainsKey(pair.Key) ? " &aOnline" : string.Empty;
                options.Add(new LabelID($"{pair.Key}: {bountyLimitHours - (Util.CurrentTime - pair.Value).Hours} Hours{online}", pair.Key));
            }

            player.svPlayer.SendOptionMenu("&6Bounties", player.ID, bountiesMenu, options.ToArray(), new LabelID[0]);
        }

        public override void OnOptionMenuAction(int targetID, string menuID, string optionID, string actionID)
        {
            if(menuID == playersMenu)
            {
                if(actionID == place) PlaceBounty(targetID, optionID);
                else if(actionID == cancel) CancelBounty(targetID, optionID);
            }
        }

        public void PlaceBounty(int sourceID, string bountyName)
        {
            ShPlayer requester = EntityCollections.FindByID<ShPlayer>(sourceID);
            if (!requester)
            {
                Debug.LogError("[SVR] Requester not found");
                return;
            }

            if (bounties.ContainsKey(bountyName))
            {
                requester.svPlayer.SendGameMessage("Bounty already exists for " + bountyName);
            }
            else if (requester.MyMoneyCount < placeCost)
            {
                requester.svPlayer.SendGameMessage("Not enough money");
            }
            else
            {
                AddBounty(bountyName);
                requester.TransferMoney(DeltaInv.RemoveFromMe, placeCost);
                OnEmployeeAction(requester, null);
            }
        }

        public void AddBounty(string bountyName)
        {
            bounties[bountyName] = Util.CurrentTime;
            ChatHandler.SendToAll("Bounty Placed on " + bountyName);
        }

        public void CancelBounty(int sourceID, string bountyName)
        {
            ShPlayer requester = EntityCollections.FindByID<ShPlayer>(sourceID);
            if (!requester)
            {
                Debug.LogError("[SVR] Requester not found");
                return;
            }

            if (!bounties.ContainsKey(bountyName))
            {
                requester.svPlayer.SendGameMessage("No Bounty for " + bountyName);
            }
            else if (requester.MyMoneyCount < cancelCost)
            {
                requester.svPlayer.SendGameMessage("Not enough money");
            }
            else
            {
                bounties.Remove(bountyName);
                ChatHandler.SendToAll("Bounty Canceled on " + bountyName);
                requester.TransferMoney(DeltaInv.RemoveFromMe, cancelCost);
                OnEmployeeAction(requester, null);

                if (aiTarget && bountyName == aiTarget.username) aiTarget = null;
            }
        }
    }

    public class Prisoner : LoopJob
    {
        public override void ResetJobAI() => player.svPlayer.SetState(StateIndex.Waypoint);
    }

    public class Police : TargetPlayerJob
    {
        protected override void FoundTarget()
        {
            base.FoundTarget();
            player.svPlayer.SendGameMessage("Criminal target: " + targetPlayer.username);
            targetPlayer.svPlayer.SendGameMessage("Police dispatched!");
        }

        public override void Loop()
        {
            if (player.IsDead) return;

            if (!player.isHuman)
            {
                if (!player.svPlayer.targetEntity && player.IsMobile &&
                    Random.value > player.svPlayer.SaturationLevel(WaypointType.Player, 30f))
                {
                    TryFindCriminal();
                }
            }
            else if (!ValidTarget(targetPlayer))
            {
                SetTarget();
            }
        }

        public override void OnJailCriminal(ShPlayer criminal, int fine) => player.svPlayer.Reward(3, fine);

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
        protected void TryFindKnockedOut()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && p.IsKnockedOut,
                (e) =>
                {
                    player.svPlayer.targetEntity = e;
                    player.svPlayer.SetState(StateIndex.Revive);
                });
        }

        public override void Loop()
        {
            if (player.IsDead) return;

            if (!player.isHuman)
            {
                if (Random.value < 0.1f && player.IsMobile && !player.svPlayer.targetEntity &&
                    player.HasItem(player.manager.defibrillator))
                {
                    TryFindKnockedOut();
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

        protected override bool ValidTarget(ShEntity target) => 
            base.ValidTarget(target) && (target as ShPlayer).IsKnockedOut;

        public override void OnHealEntity(ShEntity entity) => player.svPlayer.Reward(2, 100);

        public override void OnRevivePlayer(ShPlayer entity) => player.svPlayer.Reward(3, 250);
    }

    public class Firefighter : TargetEntityJob
    {
        public void TryFindFire()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e.gameObject.layer == LayerIndex.fire && player.CanSeeEntity(e),
                (e) =>
                {
                    player.svPlayer.targetEntity = e;
                    player.svPlayer.SetState(StateIndex.Extinguish);
                });
        }

        public override void Loop()
        {
            if (player.IsDead) return;

            if (!player.isHuman)
            {
                if (Random.value < 0.1f && player.IsMobile && !player.svPlayer.targetEntity &&
                    player.HasItem(player.manager.extinguisher))
                {
                    TryFindFire();
                }
            }
            else if (!ValidTarget(target))
            {
                SetTarget();
            }
        }

        protected override GetEntityCallback GetTargetHandler() => () =>
        {
            if (svManager.fires.Count > 0)
                return svManager.fires.ToArray().GetRandom();
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

    public class Gangster : LoopJob
    {
        protected int gangstersKilled;

        public void TryFindEnemyGang()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && !p.IsDead && p.svPlayer.job.info.shared.groupIndex == GroupIndex.Gang &&
                        p.svPlayer.job.info.shared.jobIndex != info.shared.jobIndex && !p.IsRestrained && player.CanSeeEntity(e),
                (e) => player.svPlayer.SetAttackState(e));
        }

        public override void Loop()
        {
            if (!player.isHuman && !player.svPlayer.targetEntity && !player.IsDead && Random.value < 0.01f && player.IsMobile
                && player.svPlayer.currentState.index == StateIndex.Waypoint)
            {
                TryFindEnemyGang();
            }
        }

        public override float GetSpawnRate()
        {
            ShTerritory territory = player.svPlayer.GetTerritory;

            if (territory && territory.ownerIndex == info.shared.jobIndex)
            {
                return (territory.attackerIndex == Util.invalidByte) ? info.spawnRate : 4f;
            }
            return 0f;
        }

        public override void SetJob()
        {
            if (player.isHuman)
            {
                gangstersKilled = 0;
                foreach (ShTerritory territory in svManager.territories.Values)
                {
                    territory.svEntity.AddSubscribedPlayer(player);
                }
            }
            base.SetJob();
        }

        public override void RemoveJob()
        {
            if (player.isHuman)
            {
                foreach (ShTerritory territory in svManager.territories.Values)
                {
                    territory.svEntity.RemoveSubscribedPlayer(player, true);
                }
            }
            base.RemoveJob();
        }

        public override void OnDestroyEntity(ShEntity entity)
        {
            base.OnDestroyEntity(entity);

            if (entity is ShPlayer victim)
            {
                if (!svManager.gangWar)
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
                    if (t && t.attackerIndex != Util.invalidByte)
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
                if (territory && territory.ownerIndex == info.shared.jobIndex && territory.attackerIndex != Util.invalidByte)
                {
                    if (player.svPlayer.SetAttackState(target)) return;
                }
            }
            base.ResetJobAI();
        }
    }

    public class Mayor : LoopJob
    {
        private static readonly Dictionary<string, string> requests = new Dictionary<string, string>();

        private readonly HashSet<string> requestItems = new HashSet<string>{
            "LicenseGun",
            "LicenseDrivers",
            "LicensePilots",
            "LicenseBoating"
        };

        private const string requestMenu = "requests";
        private const string itemMenu = "items";

        private const string accept = "accept";
        private const string deny = "deny";

        public override void SetJob()
        {
            if (player.isHuman) ChatHandler.SendToAll("New Mayor: " + player.username);
            base.SetJob();
        }

        public override void RemoveJob()
        {
            if (player.isHuman) ChatHandler.SendToAll("Mayor Left: " + player.username);
            base.RemoveJob();
        }

        public override void Loop()
        {
            List<string> removeKeys = new List<string>();

            foreach(string requesterName in requests.Keys.ToArray())
            {
                if (!EntityCollections.Accounts.ContainsKey(requesterName))
                {
                    removeKeys.Add(requesterName);
                }
                else if (!player.isHuman && info.members.Count == 0) 
                {
                    // AI will accept all item requests if no human Mayor present
                    ResultHandle(requesterName, accept);
                }    
            }

            foreach(string s in removeKeys) requests.Remove(s);
        }

        public override void OnEmployeeAction(ShPlayer target, string actionID)
        {
            List<LabelID> options = new List<LabelID>();

            foreach (string s in requestItems)
            {
                ShItem item = SceneManager.Instance.GetEntity<ShItem>(s.GetPrefabIndex());

                if (item)
                {
                    options.Add(new LabelID($"{item.itemName} &6{item.value}", s));
                }
            }

            // Negative playerID means job action is called on the employer with that ID, not self
            target.svPlayer.SendOptionMenu("&7Items", -player.ID, itemMenu, options.ToArray(), new LabelID[] { new LabelID("Request", string.Empty) }); 
        }

        public override void OnSelfAction(string actionID)
        {
            List<LabelID> options = new List<LabelID>();

            foreach (KeyValuePair<string, string> pair in requests)
            {
                ShItem i = SceneManager.Instance.GetEntity<ShItem>(pair.Value);
                if (i)
                {
                    options.Add(new LabelID($"{pair.Key}: &6{i.itemName}", pair.Key));
                }
            }

            player.svPlayer.SendOptionMenu("&7Requests", player.ID, requestMenu, options.ToArray(), new LabelID[] { new LabelID("Accept", accept), new LabelID("Deny", deny) });
        }


        public override void OnOptionMenuAction(int targetID, string menuID, string optionID, string actionID)
        {
            switch(menuID)
            {
                case itemMenu:
                    RequestAdd(targetID, optionID); // actionID doesn't matter here
                    break;
                case requestMenu:
                    ResultHandle(optionID, actionID); // targetID can only be self here
                    break;
                default:
                    break;
            }
        }

        public void RequestAdd(int sourceID, string itemName)
        {
            if (!requestItems.Contains(itemName))
            {
                Debug.LogError("[SVR] Item not valid: " + itemName);
                return;
            }

            ShItem item = SceneManager.Instance.GetEntity<ShItem>(itemName);
            if (!item)
            {
                Debug.LogError("[SVR] Item not found: " + itemName);
                return;
            }

            ShPlayer requester = EntityCollections.FindByID<ShPlayer>(sourceID);
            if(!requester)
            {
                Debug.LogError("[SVR] Requester not found");
                return;
            }

            if(requester.HasItem(item.index))
            {
                requester.svPlayer.SendGameMessage("Already own item");
            }
            else if (requester.MyMoneyCount < item.value)
            {
                requester.svPlayer.SendGameMessage("Not enough money");
            }
            else if (requests.ContainsKey(requester.username))
            {
                requester.svPlayer.SendGameMessage("Previous request still pending");
            }
            else
            {
                requests[requester.username] = itemName;
                requester.svPlayer.SendGameMessage("Request successfully sent");
                ShPlayer mayor = info.members.FirstOrDefault();
                if (mayor)
                {
                    mayor.svPlayer.SendGameMessage(requester.username + " requesting a " + item.itemName);
                }
            }
        }

        public void ResultHandle(string requesterName, string result)
        {
            if (!requests.TryGetValue(requesterName, out string itemName))
            {
                Debug.LogError("[SVR] Requester invalid: " + requesterName);
                return;
            }

            if (EntityCollections.Accounts.TryGetValue(requesterName, out ShPlayer requester))
            {
                if (result == accept)
                {
                    ShItem item = SceneManager.Instance.GetEntity<ShItem>(itemName);
                    if (item)
                    {
                        if (requester.MyMoneyCount >= item.value)
                        {
                            requester.TransferMoney(DeltaInv.RemoveFromMe, item.value);
                            requester.TransferItem(DeltaInv.AddToMe, item);
                            player.TransferMoney(DeltaInv.AddToMe, item.value);
                        }
                        else
                        {
                            player.svPlayer.SendGameMessage("Player missing funds");
                            requester.svPlayer.SendGameMessage("No funds for license");
                        }
                    }
                }
                else if (result == deny)
                {
                    requester.svPlayer.SendGameMessage("License Denied");
                }
            }

            requests.Remove(requesterName);
            OnSelfAction(null);
        }
    }

    public abstract class TargetEntityJob : LoopJob
    {
        [NonSerialized]
        public ShEntity target;

        protected delegate ShEntity GetEntityCallback();

        protected virtual bool ValidTarget(ShEntity target) => 
            target && target != player && target.isActiveAndEnabled && !target.IsDead;

        public virtual void ResetTarget()
        {
            player.svPlayer.DestroyGoalMarker();
            target = null;
        }

        protected virtual void FoundTarget() => player.svPlayer.StartGoalMarker(target);

        protected abstract GetEntityCallback GetTargetHandler();

        protected bool SetTarget()
        {
            ResetTarget();

            GetEntityCallback handler = GetTargetHandler();

            for (int i = 0; i < 20; i++)
            {
                ShEntity e = handler();

                if (ValidTarget(e))
                {
                    target = e;
                    FoundTarget();
                    return true;
                }
            }

            return false;
        }

        public override void RemoveJob()
        {
            if(player.isHuman) ResetTarget();
            base.RemoveJob();
        }
    }

    public abstract class TargetPlayerJob : TargetEntityJob
    {
        [NonSerialized]
        public ShPlayer targetPlayer;

        protected bool SetSpawnTarget()
        {
            ShPlayer target = player.svPlayer.spawner;

            if (target && target.IsOutside && target.wantedLevel >= info.attackLevel &&
                Random.value < target.wantedNormalized && player.DistanceSqr(target) <= Util.visibleRangeSqr)
            {
                return player.svPlayer.SetAttackState(target);
            }
            return false;
        }

        protected void TryFindCriminal()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && !p.IsDead && !p.IsRestrained && p.wantedLevel >= info.attackLevel && player.CanSeeEntity(e),
                (e) => player.svPlayer.SetAttackState(e));
        }

        protected override void FoundTarget()
        {
            base.FoundTarget();
            targetPlayer = target as ShPlayer;
        }

        protected override bool ValidTarget(ShEntity target) => 
            base.ValidTarget(target) && (target as ShPlayer).wantedLevel >= info.attackLevel;

        protected override GetEntityCallback GetTargetHandler()
        {
            if (EntityCollections.Humans.Count >= 3)
            {
                return () => EntityCollections.RandomHuman;
            }
            else
            {
                return () => EntityCollections.RandomNPC;
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
            player.svPlayer.SendGameMessage("High-value target: " + targetPlayer.username);
            targetPlayer.svPlayer.SendGameMessage("SpecOps dispatched!");
        }

        public override void Loop()
        {
            if (player.IsDead) return;

            if (!player.isHuman)
            {
                if (!player.svPlayer.targetEntity && player.IsMobile &&
                    Random.value > player.svPlayer.SaturationLevel(WaypointType.Player, 30f))
                {
                    TryFindCriminal();
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

        protected override bool ValidTarget(ShEntity target) => 
            base.ValidTarget(target) && (target is ShPlayer p) && !p.curMount && !(p.svPlayer.job is Prisoner);

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
            if (player.isHuman && !player.IsDead)
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

        public override void OnSpecialAction(ShEntity target, string actionID)
        {
            if (!target || target.IsDead || !player.InActionRange(target)) return;

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

        protected override GetEntityCallback GetTargetHandler() => () => EntityCollections.RandomNPC;

        protected bool MountWithinReach(ShEntity e)
        {
            ShMountable m = player.GetMount;
            return m.Velocity.sqrMagnitude <= Util.slowSpeedSqr && e.InActionRange(m);
        }

        public override void Loop()
        {
            if (player.isHuman && !player.IsDead)
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
                    else if (MountWithinReach(destinationMarker))
                    {
                        player.svPlayer.Reward(2, Mathf.CeilToInt(timeDeadline - Time.time));
                        SetTarget();
                    }
                }
                else if (
                    targetPlayer && MountWithinReach(targetPlayer) &&
                    targetPlayer.svPlayer.SvMountBack(player.curMount.ID, false))
                {
                    player.svPlayer.DestroyGoalMarker();

                    SpawnLocation destination = svManager.spawnLocations.GetRandom();

                    destinationMarker = svManager.AddNewEntity(
                        svManager.markerGoalPrefab,
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
