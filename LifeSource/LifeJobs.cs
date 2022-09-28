using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Jobs;
using BrokeProtocol.Utility.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BrokeProtocol.GameSource
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum GroupIndex
    {
        Citizen,
        Criminal,
        LawEnforcement,
        Prisoner
    }

    public struct Transports
    {
        public string[] transports;
        public Transports(string[] transports) { this.transports = transports; }
    }

    public class MyJobInfo : JobInfo
    {
        public readonly GroupIndex groupIndex;
        public readonly float spawnRate;
        public readonly int poolSize;
        public Transports[] transports;
        public HashSet<ShEntity>[] randomEntities;

        public MyJobInfo(
            Type jobType,
            string jobName,
            string jobDescription,
            CharacterType characterType,
            int maxCount,
            GroupIndex groupIndex,
            ColorStruct jobColor,
            float spawnRate,
            int poolSize,
            Transports[] transports,
            Upgrades[] upgrades) : base(jobType, jobName, jobDescription, characterType, maxCount, jobColor, upgrades)
        {
            this.groupIndex = groupIndex;
            this.spawnRate = spawnRate;
            this.poolSize = poolSize;
            this.transports = transports;
        }
    }



    public class JobLife : Job
    {
        public override void ResetJobAI()
        {
            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                if (player.svPlayer.stop &&
                    pluginPlayer.SetGoToState(
                        player.svPlayer.originalPosition,
                        player.svPlayer.originalRotation,
                        player.svPlayer.originalParent))
                    return;

                if (player.svPlayer.currentState.index == Core.Freeze.index && player.svPlayer.SetState(Core.Flee.index))
                    return;

                if (player.characterType != CharacterType.Humanoid && player.svPlayer.SetState(Core.Wander.index))
                    return;

                if (player.svPlayer.SetState(Core.Waypoint.index))
                    return;
            }

            base.ResetJobAI();
        }

        public override bool IsValidTarget(ShPlayer chaser)
        {
            return LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer) &&
                (
                (chaser.svPlayer.IsFollower(player) || ((MyJobInfo)chaser.svPlayer.job.info).groupIndex != GroupIndex.LawEnforcement || (!player.IsRestrained && pluginPlayer.wantedLevel > 0)) &&
                (!chaser.curMount || player.GetPlaceIndex == chaser.GetPlaceIndex)
                );
        }

        public override ShUsable GetBestJobWeapon()
        {
            if (((MyJobInfo)info).groupIndex == GroupIndex.LawEnforcement && player.svPlayer.targetEntity is ShPlayer targetPlayer &&
                LifeManager.pluginPlayers.TryGetValue(targetPlayer, out var pluginTarget) && pluginTarget.wantedLevel <= 1 && player.HasItem(targetPlayer.Handcuffs))
            {
                return targetPlayer.Handcuffs;
            }

            return null;
        }

        public override void OnDamageEntity(ShEntity damaged)
        {
            base.OnDamageEntity(damaged);
            if (damaged is ShPlayer victim && LifeManager.pluginPlayers.TryGetValue(victim, out var pluginVictim) && 
                pluginVictim.wantedLevel == 0 && LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                if (victim.characterType == CharacterType.Mob)
                {
                    pluginPlayer.AddCrime(CrimeIndex.AnimalCruelty, victim);
                }
                else if (player.curEquipable is ShGun)
                {
                    pluginPlayer.AddCrime(CrimeIndex.ArmedAssault, victim);
                }
                else
                {
                    pluginPlayer.AddCrime(CrimeIndex.Assault, victim);
                }
            }
        }

        public override void OnDestroyEntity(ShEntity destroyed)
        {
            base.OnDestroyEntity(destroyed);
            var victim = destroyed.Player;
            if (victim && LifeManager.pluginPlayers.TryGetValue(victim, out var pluginVictim) && 
                pluginVictim.wantedLevel == 0 && LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.AddCrime(victim.characterType == CharacterType.Humanoid ? CrimeIndex.Murder : CrimeIndex.AnimalKilling, victim);

                if (victim.isHuman && player.isHuman)
                {
                    victim.svPlayer.SendGameMessage(player.username + " murdered " + victim.username);
                }
            }
        }

        public override float GetSpawnRate() => ((MyJobInfo)info).spawnRate;
    }


    public abstract class LoopJob : JobLife
    {
        public override void ResetJob()
        {
            base.ResetJob();
            RestartCoroutines();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            RestartCoroutines();
        }

        private void RestartCoroutines()
        {
            if (player.isActiveAndEnabled && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                if (pluginPlayer.jobCoroutine != null) player.StopCoroutine(pluginPlayer.jobCoroutine);
                pluginPlayer.jobCoroutine = player.StartCoroutine(JobCoroutine());
            }
        }

        private IEnumerator JobCoroutine()
        {
            var delay = new WaitForSeconds(1f);
            do
            {
                yield return delay;
                Loop();
            } while (true);
        }

        public abstract void Loop();
    }


    public class Citizen : LoopJob
    {
        protected void TryFindInnocent()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && LifeManager.pluginPlayers.TryGetValue(p, out var pluginPlayer) && !p.curMount && 
                !p.IsDead && p.IsRestrained && pluginPlayer.wantedLevel == 0 && player.CanSeeEntity(e),
                (e) =>
                {
                    player.svPlayer.targetEntity = e;
                    player.svPlayer.SetState(Core.Free.index);
                });
        }

        public void TryFindVictim()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && !p.curMount && p.IsCapable && player.CanSeeEntity(e),
                (e) =>
                {
                    player.svPlayer.targetEntity = e;
                    player.svPlayer.SetState(LifeCore.Rob.index);
                });
        }

        public override void Loop()
        {
            if (!player.isHuman && !player.svPlayer.targetEntity && !player.curMount && player.IsMobile && 
                player.svPlayer.currentState.index == Core.Waypoint.index)
            {
                var rand = Random.value;

                if (rand < 0.003f) TryFindVictim();
                else if (rand < 0.015f) TryFindInnocent();
            }
        }
    }

    public class Hitman : LoopJob
    {
        public const string bountiesKey = "bounties";
        public static readonly Dictionary<string, DateTimeOffset> bounties = new Dictionary<string, DateTimeOffset>();
        public static ShPlayer aiTarget;

        public const string placeBountyMenu = "PlaceBounty";
        public const string bountyListMenu = "BountyList";
        public const string bountyListSelfMenu = "BountyListSelf";

        private const string place = "place";
        private const string cancel = "cancel";

        private const int placeCost = 2000;
        private const int cancelCost = 3000;

        private const float bountyLimitHours = 100f;

        public override void SetJob()
        {
            player.svPlayer.SvAddDynamicAction(placeBountyMenu, "Place Bounty");
            player.svPlayer.SvAddDynamicAction(bountyListMenu, "Bounty List");
            player.svPlayer.SvAddSelfAction(bountyListSelfMenu, "Bounty List");
            base.SetJob();
        }

        public override void RemoveJob()
        {
            player.svPlayer.SvRemoveDynamicAction(placeBountyMenu);
            player.svPlayer.SvRemoveDynamicAction(bountyListMenu);
            player.svPlayer.SvRemoveSelfAction(bountyListSelfMenu);
            base.RemoveJob();
        }

        protected void TryFindBounty()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && (p.svPlayer.job is SpecOps || bounties.ContainsKey(p.username)) && player.CanSeeEntity(e, true),
                (e) =>
                {
                    if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer) && 
                    LifeManager.pluginPlayers.TryGetValue(player, out var lifeSourcePlayer))
                    {
                        // Add random crimes to ensure high wanted level (targetable by SpecOps)
                        while (lifeSourcePlayer.wantedLevel < 3)
                        {
                            lifeSourcePlayer.AddCrime(Util.RandomEnumValue<CrimeIndex>(), e as ShPlayer);
                        }
                        pluginPlayer.SetAttackState(e);
                    }
                });
        }

        public override void OnDestroyEntity(ShEntity destroyed)
        {
            base.OnDestroyEntity(destroyed);
            var victim = destroyed.Player;
            if (victim && bounties.ContainsKey(victim.username))
            {
                player.svPlayer.Reward(3, 1000);
                bounties.Remove(victim.username);
                InterfaceHandler.SendGameMessageToAll($"{player.username} assassinated {victim.username}");

                if (victim == aiTarget) aiTarget = null;
            }
        }

        public override void Loop()
        {
            var removeKeys = new List<string>();

            foreach (var pair in bounties)
            {
                if ((Util.CurrentTime - pair.Value).Hours >= bountyLimitHours)
                {
                    removeKeys.Add(pair.Key);
                }
            }

            foreach (var s in removeKeys) bounties.Remove(s);

            if (player.IsDead) return;

            if (!player.isHuman)
            {
                if (!player.svPlayer.targetEntity && Random.value < 0.02f && player.IsMobile && player.svPlayer.currentState.index == Core.Waypoint.index)
                {
                    TryFindBounty();
                }
            }
            else if (!aiTarget)
            {
                aiTarget = EntityCollections.RandomNPC;

                if (aiTarget) AddBounty(aiTarget.username);
            }
        }

        public void PlaceBountyAction(ShPlayer requester)
        {
            var options = new List<LabelID>();

            foreach (var p in EntityCollections.Humans)
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

            // Negative playerID means job action is called on the employee with that ID, not self
            requester.svPlayer.SendOptionMenu("&6Players", -player.ID, placeBountyMenu, options.ToArray(), new LabelID[] { new LabelID($"Place Bounty ${placeCost}", place) });
        }

        public void BountyListAction(ShPlayer requester)
        {
            var options = new List<LabelID>();

            foreach (var pair in bounties)
            {
                var online = EntityCollections.Accounts.ContainsKey(pair.Key) ? " &aOnline" : string.Empty;
                options.Add(new LabelID($"{pair.Key}: {bountyLimitHours - (Util.CurrentTime - pair.Value).Hours} Hours{online}", pair.Key));
            }

            requester.svPlayer.SendOptionMenu("&6Bounties", -player.ID, bountyListMenu, options.ToArray(), new LabelID[] { new LabelID($"Cancel Bounty ${cancelCost}", cancel) });
        }

        public override void OnOptionMenuAction(int targetID, string menuID, string optionID, string actionID)
        {
            base.OnOptionMenuAction(targetID, menuID, optionID, actionID);
            switch(menuID)
            {
                case placeBountyMenu:
                    PlaceBounty(targetID, optionID);
                    break;

                case bountyListMenu:
                case bountyListSelfMenu:
                    CancelBounty(targetID, optionID);
                    break;
            }
        }

        public void PlaceBounty(int sourceID, string bountyName)
        {
            var requester = EntityCollections.FindByID<ShPlayer>(sourceID);
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
                PlaceBountyAction(requester);
            }
        }

        public void AddBounty(string bountyName)
        {
            bounties[bountyName] = Util.CurrentTime;
            InterfaceHandler.SendGameMessageToAll("Bounty Placed on " + bountyName);
        }

        public void CancelBounty(int sourceID, string bountyName)
        {
            var requester = EntityCollections.FindByID<ShPlayer>(sourceID);
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
                InterfaceHandler.SendGameMessageToAll("Bounty Canceled on " + bountyName);
                requester.TransferMoney(DeltaInv.RemoveFromMe, cancelCost);
                BountyListAction(requester);

                if (aiTarget && bountyName == aiTarget.username) aiTarget = null;
            }
        }
    }

    public class Prisoner : JobLife
    {
        public override void ResetJobAI()
        {
            if (!player.svPlayer.SetState(Core.Wander.index))
            {
                base.ResetJobAI();
            }
        }
    }

    public class Police : LawEnforcement
    {
        public const string drugTest = "DrugTest";

        public override void SetJob()
        {
            player.svPlayer.SvAddTypeAction(drugTest, "ShPlayer", "Drug Test");
            base.SetJob();
        }

        public override void RemoveJob()
        {
            player.svPlayer.SvRemoveTypeAction(drugTest);
            base.RemoveJob();
        }

        protected override void FoundTarget(bool startGoalMarker)
        {
            base.FoundTarget(startGoalMarker);
            player.svPlayer.SendGameMessage("Criminal target: " + targetPlayer.username);
            targetPlayer.svPlayer.SendGameMessage("Police dispatched!");
        }
    }

    public class Paramedic : TargetPlayerJob
    {
        public const string requestHeal = "RequestHeal";

        public override void SetJob()
        {
            player.svPlayer.SvAddDynamicAction(requestHeal, "Request Heal");
            base.SetJob();
        }

        public override void RemoveJob()
        {
            player.svPlayer.SvRemoveDynamicAction(requestHeal);
            base.RemoveJob();
        }

        public void RequestHeal(ShPlayer requester)
        {
            if (!requester.CanHeal)
            {
                requester.svPlayer.SendGameMessage("Cannot heal");
            }
            else if (player.isHuman)
            {
                player.svPlayer.SendGameMessage($"{requester.username} is requesting healing");
            }
            else if(!player.isActiveAndEnabled || !player.IsUp || player.svPlayer.currentState.IsBusy)
            {
                requester.svPlayer.SendGameMessage("NPC is occupied");
            }
            else
            {
                player.svPlayer.targetEntity = requester;
                player.svPlayer.SetState(Core.Heal.index);
            }
        }

        protected void TryFindKnockedOut()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && p.IsKnockedOut,
                (e) =>
                {
                    player.svPlayer.targetEntity = e;
                    player.svPlayer.SetState(Core.Revive.index);
                });
        }

        public override void Loop()
        {
            if (player.IsDead) return;

            if (!player.isHuman)
            {
                if (Random.value < 0.02f && player.IsMobile && !player.svPlayer.targetEntity && player.HasItem(player.manager.defibrillator))
                {
                    TryFindKnockedOut();
                }
            }
            else if (!ValidTarget(target))
            {
                SetTarget();
            }
        }

        protected override void FoundTarget(bool startGoalMarker)
        {
            base.FoundTarget(startGoalMarker);
            player.svPlayer.SendGameMessage(targetPlayer.username + " has been knocked out! Check map");
            targetPlayer.svPlayer.SendGameMessage("Paramedic alerted to your location");
        }

        protected override bool ValidTarget(ShEntity target) => base.ValidTarget(target) && target is ShPlayer p && p.IsKnockedOut;

        public override void OnHealEntity(ShEntity entity)
        {
            base.OnHealEntity(entity);
            // Make sure not a transport being fixed
            if(entity is ShPlayer) player.svPlayer.Reward(2, 100);
        }

        public override void OnRevivePlayer(ShPlayer player)
        {
            base.OnRevivePlayer(player);
            base.player.svPlayer.Reward(3, 250);
        }
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
                    player.svPlayer.SetState(Core.Extinguish.index);
                });
        }

        public override void Loop()
        {
            if (player.IsDead) return;

            if (!player.isHuman)
            {
                if (Random.value < 0.02f && player.IsMobile && !player.svPlayer.targetEntity && player.HasItem(player.manager.extinguisher))
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
            if (SvManager.Instance.fires.Count > 0)
                return SvManager.Instance.fires.ToArray().GetRandom();
            else
                return null;
        };

        protected override void FoundTarget(bool startGoalMarker)
        {
            base.FoundTarget(startGoalMarker);
            player.svPlayer.SendGameMessage("Fire reported! Check Map");
        }

        public void CheckReward(ShEntity e)
        {
            if (e.svEntity.spawner != player && e.gameObject.layer == LayerIndex.fire)
            {
                player.svPlayer.Reward(1, 25);
            }
        }

        public override void OnDamageEntity(ShEntity damaged)
        {
            CheckReward(damaged);
            base.OnDamageEntity(damaged);
        }

        public override void OnDestroyEntity(ShEntity destroyed)
        {
            CheckReward(destroyed);
            base.OnDestroyEntity(destroyed);
        }
    }

    public class Gangster : LoopJob
    {
        protected int gangstersKilled;

        public void TryFindEnemyGang()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && p.IsCapable && p.svPlayer.job is Gangster &&
                        p.svPlayer.job.info.shared.jobIndex != info.shared.jobIndex && player.CanSeeEntity(e, true),
                (e) =>
                {
                    if(Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                        pluginPlayer.SetAttackState(e);
                });
        }

        public override void Loop()
        {
            if (!player.isHuman && player.IsMobile && !player.svPlayer.currentState.IsBusy &&
                (LifeManager.warTerritory?.ownerIndex == info.shared.jobIndex || Random.value < 0.01f))
            {
                TryFindEnemyGang();
            }
        }

        public override float GetSpawnRate()
        {
            // Use the spawner territory to calculate spawn rate (better AI defence spawning during gangwars)
            if (Manager.TryGetTerritory(player.svPlayer.spawner, out var territory) && territory.ownerIndex == info.shared.jobIndex)
            {
                // Boost gangster spawn rate if territory under attack
                return (territory.attackerIndex < 0) ? ((MyJobInfo)info).spawnRate : 8f;
            }
            return 0f;
        }

        public override void SetJob()
        {
            if (player.isHuman)
            {
                gangstersKilled = 0;
                foreach (var territory in Manager.territories)
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
                foreach (var territory in Manager.territories)
                {
                    territory.svEntity.RemoveSubscribedPlayer(player, true);
                }
            }
            base.RemoveJob();
        }

        protected bool IsEnemyGangster(ShPlayer target) => target.svPlayer.job is Gangster && this != target.svPlayer.job;

        public override void OnDamageEntity(ShEntity damaged)
        {
            if (!(damaged is ShPlayer victim) || !IsEnemyGangster(victim))
                base.OnDamageEntity(damaged);
        }

        public override void OnDestroyEntity(ShEntity destroyed)
        {
            var victim = destroyed.Player;
            if (victim && IsEnemyGangster(victim))
            {
                if (!LifeManager.warTerritory)
                {
                    if (player.isHuman)
                    {
                        if (Manager.TryGetTerritory(player, out var t) && t.ownerIndex != info.shared.jobIndex && gangstersKilled >= 1)
                        {
                            LifeManager.StartGangWar(t, info.shared.jobIndex);
                            gangstersKilled = 0;
                        }
                        else
                        {
                            gangstersKilled++;
                        }

                        player.svPlayer.Reward(2, 50);
                    }
                }
                else if (Manager.TryGetTerritory(player, out var t) && t.attackerIndex >= 0)
                {
                    if (victim.svPlayer.job.info.shared.jobIndex == t.ownerIndex)
                    {
                        LifeManager.defendersKilled++;
                        LifeManager.SendTerritoryStats();
                        player.svPlayer.Reward(3, 100);
                    }
                    else if (victim.svPlayer.job.info.shared.jobIndex == t.attackerIndex)
                    {
                        LifeManager.attackersKilled++;
                        LifeManager.SendTerritoryStats();
                        player.svPlayer.Reward(3, 100);
                    }
                }
            }
            else
            {
                base.OnDestroyEntity(destroyed);
            }
        }

        public override void ResetJobAI()
        {
            var target = player.svPlayer.spawner;

            if (target && target.IsOutside && target.svPlayer.job is Gangster &&
                target.svPlayer.job != this && player.DistanceSqr(target) <= Util.visibleRangeSqr &&
                Manager.TryGetTerritory(target, out var territory) && territory.ownerIndex == info.shared.jobIndex &&
                Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer) &&
                territory.attackerIndex >= 0 && pluginPlayer.SetAttackState(target))
            {
                return;
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

        private const string requestItemMenu = "RequestItem";
        private const string requestListMenu = "RequestList";

        private const string accept = "accept";
        private const string deny = "deny";

        public override void SetJob()
        {
            player.svPlayer.SvAddDynamicAction(requestItemMenu, "Request Item");
            player.svPlayer.SvAddSelfAction(requestListMenu, "Request List");

            if (player.isHuman) InterfaceHandler.SendGameMessageToAll("New Mayor: " + player.username);
            base.SetJob();
        }

        public override void RemoveJob()
        {
            player.svPlayer.SvRemoveDynamicAction(requestItemMenu);
            player.svPlayer.SvRemoveSelfAction(requestListMenu);

            if (player.isHuman) InterfaceHandler.SendGameMessageToAll("Mayor Left: " + player.username);
            base.RemoveJob();
        }

        public override void Loop()
        {
            var removeKeys = new List<string>();

            foreach(var requesterName in requests.Keys.ToArray())
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

            foreach(var s in removeKeys) requests.Remove(s);
        }

        public void RequestItemAction(ShPlayer target)
        {
            var options = new List<LabelID>();

            foreach (var s in requestItems)
            {
                var item = SceneManager.Instance.GetEntity<ShItem>(s.GetPrefabIndex());

                if (item)
                {
                    options.Add(new LabelID($"{item.itemName} &6${item.value}", s));
                }
            }

            // Negative playerID means job action is called on the employer with that ID, not self
            target.svPlayer.SendOptionMenu("&7Items", -player.ID, requestItemMenu, options.ToArray(), new LabelID[] { new LabelID("Request", string.Empty) }); 
        }

        public void RequestListAction()
        {
            var options = new List<LabelID>();

            foreach (var pair in requests)
            {
                var i = SceneManager.Instance.GetEntity<ShItem>(pair.Value);
                if (i)
                {
                    options.Add(new LabelID($"{pair.Key}: &6{i.itemName}", pair.Key));
                }
            }

            player.svPlayer.SendOptionMenu("&7Requests", player.ID, requestListMenu, options.ToArray(), new LabelID[] { new LabelID("Accept", accept), new LabelID("Deny", deny) });
        }


        public override void OnOptionMenuAction(int targetID, string menuID, string optionID, string actionID)
        {
            base.OnOptionMenuAction(targetID, menuID, optionID, actionID);
            switch (menuID)
            {
                case requestItemMenu:
                    RequestAdd(targetID, optionID); // actionID doesn't matter here
                    break;
                case requestListMenu:
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

            var item = SceneManager.Instance.GetEntity<ShItem>(itemName);
            if (!item)
            {
                Debug.LogError("[SVR] Item not found: " + itemName);
                return;
            }

            var requester = EntityCollections.FindByID<ShPlayer>(sourceID);
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
                var mayor = info.members.FirstOrDefault();
                if (mayor)
                {
                    mayor.svPlayer.SendGameMessage(requester.username + " requesting a " + item.itemName);
                }
            }
        }

        public void ResultHandle(string requesterName, string result)
        {
            if (!requests.TryGetValue(requesterName, out var itemName))
            {
                Debug.LogError("[SVR] Requester invalid: " + requesterName);
                return;
            }

            if (EntityCollections.Accounts.TryGetValue(requesterName, out var requester))
            {
                if (result == accept)
                {
                    var item = SceneManager.Instance.GetEntity<ShItem>(itemName);
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
            RequestListAction();
        }
    }

    public abstract class TargetEntityJob : LoopJob
    {
        [NonSerialized]
        public ShEntity target;

        protected delegate ShEntity GetEntityCallback();

        public const string resetTarget = "ResetTarget";

        public override void SetJob()
        {
            player.svPlayer.SvAddSelfAction(resetTarget, "Reset Target");
            base.SetJob();
        }

        public override void RemoveJob()
        {
            player.svPlayer.SvRemoveSelfAction(resetTarget);
            if (player.isHuman) ResetTarget();
            base.RemoveJob();
        }

        protected virtual bool ValidTarget(ShEntity target) => 
            target && target != player && target.isActiveAndEnabled && !target.IsDead;

        public virtual void ResetTarget()
        {
            player.svPlayer.DestroyGoalMarker();
            target = null;
        }

        protected virtual void FoundTarget(bool startGoalMarker)
        {
            if(startGoalMarker) player.svPlayer.StartGoalMarker(target);
        }

        protected abstract GetEntityCallback GetTargetHandler();

        protected bool SetTarget(bool startGoalMarker = true)
        {
            ResetTarget();

            var handler = GetTargetHandler();

            for (int i = 0; i < 20; i++)
            {
                var e = handler();

                if (ValidTarget(e))
                {
                    target = e;
                    FoundTarget(startGoalMarker);
                    return true;
                }
            }

            return false;
        }
    }

    public abstract class TargetPlayerJob : TargetEntityJob
    {
        [NonSerialized]
        public ShPlayer targetPlayer;

        public virtual int AttackLevel => 0;

        protected bool SetSpawnTarget()
        {
            var target = player.svPlayer.spawner;

            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer) &&
                target && LifeManager.pluginPlayers.TryGetValue(target, out var pluginTarget) && target.IsOutside && pluginTarget.wantedLevel >= AttackLevel &&
                Random.value < pluginTarget.wantedNormalized && player.DistanceSqr(target) <= Util.visibleRangeSqr)
            {
                return pluginPlayer.SetAttackState(target);
            }
            return false;
        }

        protected void TryFindCriminal()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && p.IsCapable && LifeManager.pluginPlayers.TryGetValue(p, out var pluginTarget) && pluginTarget.wantedLevel >= AttackLevel && player.CanSeeEntity(e, true),
                (e) =>
                {
                    if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                        pluginPlayer.SetAttackState(e);
                });
        }

        protected override void FoundTarget(bool startGoalMarker)
        {
            base.FoundTarget(startGoalMarker);
            targetPlayer = target as ShPlayer;
        }

        protected override bool ValidTarget(ShEntity target) => 
            base.ValidTarget(target) && LifeManager.pluginPlayers.TryGetValue(target, out var pluginTarget) && pluginTarget.wantedLevel >= AttackLevel;

        protected override GetEntityCallback GetTargetHandler()
        {
            if (EntityCollections.Humans.Count >= 3)
                return () => EntityCollections.RandomHuman;
            
            return () => EntityCollections.RandomNPC;
        }

        public override void ResetTarget()
        {
            targetPlayer = null;
            base.ResetTarget();
        }
    }

    public class SpecOps : LawEnforcement
    {
        public override int AttackLevel => 3;

        protected override void FoundTarget(bool startGoalMarker)
        {
            base.FoundTarget(startGoalMarker);
            player.svPlayer.SendGameMessage("High-value target: " + targetPlayer.username);
            targetPlayer.svPlayer.SendGameMessage("SpecOps dispatched!");
        }

        public override void OnDestroyEntity(ShEntity destroyed)
        {
            base.OnDestroyEntity(destroyed);
            var victim = destroyed.Player;
            if (victim && LifeManager.pluginPlayers.TryGetValue(victim, out var pluginVictim) && targetPlayer == victim && pluginVictim.wantedLevel > 0 && pluginVictim.wantedLevel >= AttackLevel)
            {
                player.svPlayer.Reward(3, 300);
            }
        }
    }

    public class DeliveryMan : TargetPlayerJob
    {
        [NonSerialized]
        public ShConsumable deliveryItem;

        [NonSerialized]
        public float timeDeadline;

        private const string deliverItemAction = "DeliverItem";

        public override void SetJob()
        {
            player.svPlayer.SvAddTypeAction(deliverItemAction, "ShPlayer", "Deliver Item");

            base.SetJob();
        }

        public override void RemoveJob()
        {
            player.svPlayer.SvRemoveTypeAction(deliverItemAction);

            base.RemoveJob();
        }

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
                player.svPlayer.DestroyText();
            }

            base.ResetTarget();
        }

        protected override void FoundTarget(bool startGoalMarker)
        {
            base.FoundTarget(startGoalMarker);
            player.svPlayer.SendGameMessage("Delivery target: " + targetPlayer.username);
            deliveryItem = SceneManager.Instance.consumablesCollection.GetRandom().Value;
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

        public void DeliverItemAction(ShEntity target)
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

        override protected bool ValidTarget(ShEntity target)
        {
            if (base.ValidTarget(target) && target.IsOutside && target is ShPlayer p && p.IsMobile && !(p.svPlayer.job is Prisoner))
            {
                if (destinationMarker)
                    return p.curMount && p.curMount == player.curMount;
                else
                    return !p.curMount;
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
                player.svPlayer.DestroyText();
            }

            base.ResetTarget();
        }

        protected override GetEntityCallback GetTargetHandler() => () => EntityCollections.RandomNPC;

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
                    else if (destinationMarker.MountWithinReach(player))
                    {
                        player.svPlayer.Reward(2, Mathf.CeilToInt(timeDeadline - Time.time));
                        SetTarget();
                    }
                }
                else if (
                    targetPlayer && targetPlayer.MountWithinReach(player) &&
                    targetPlayer.svPlayer.SvTryMount(player.curMount.ID, false))
                {
                    player.svPlayer.DestroyGoalMarker();

                    var destination = Manager.spawnLocations.GetRandom();

                    destinationMarker = SvManager.Instance.AddNewEntity(
                        SvManager.Instance.markerGoalPrefab,
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

        protected override void FoundTarget(bool startGoalMarker)
        {
            base.FoundTarget(startGoalMarker);
            player.svPlayer.SendGameMessage("Pickup target: " + targetPlayer.username);
        }
    }



    public class LawEnforcement : TargetPlayerJob
    {
        public const string sendToJail = "SendToJail";
        public const string showCrimes = "ShowCrimes";

        public override int AttackLevel => 1;

        public override void SetJob()
        {
            player.svPlayer.SvAddTypeAction(showCrimes, "ShPlayer", "Show Crimes");
            player.svPlayer.SvAddTypeAction(sendToJail, "ShPlayer", "Send to Jail");
            base.SetJob();
        }

        public override void RemoveJob()
        {
            player.svPlayer.SvRemoveTypeAction(showCrimes);
            player.svPlayer.SvRemoveTypeAction(sendToJail);
            base.RemoveJob();
        }

        public override void Loop()
        {
            if (player.IsDead) return;

            if (!player.isHuman)
            {
                if (!player.svPlayer.targetEntity && player.IsMobile && Random.value > player.svPlayer.SaturationLevel(WaypointType.Player, 30f))
                {
                    TryFindCriminal();
                }
            }
            else if (!ValidTarget(targetPlayer))
            {
                SetTarget();
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



    public class Retriever : TargetPlayerJob
    {
        private enum Stage
        {
            NotSet,
            Collecting,
            Delivering
        }

        private Stage stage;
        private ShEntity worldItem;
        private InventoryStruct[] collectedItems;
        private float timeDeadline;

        override protected bool ValidTarget(ShEntity target)
        {
            if (base.ValidTarget(target) && target is ShPlayer p && !(p.svPlayer.job is Prisoner))
            {
                switch (stage)
                {
                    case Stage.NotSet:
                        if (p.isHuman)
                        {
                            var i = p.svPlayer.spawnedEntities.GetRandom();

                            if (i && i.CollectedItems?.Length > 0)
                            {
                                worldItem = i;
                                player.svPlayer.StartGoalMarker(worldItem);
                                return true;
                            }
                        }
                        else if (p.myItems.Count > 0)
                        {
                            if(LifeManager.worldWaypoints[0].spawns.TryGetValue(player.svPlayer.sector.tuple, out var spawns))
                            {
                                var randomSpawn = spawns.GetRandom();

                                if (randomSpawn != null)
                                {
                                    worldItem = SvManager.Instance.AddNewEntity(
                                        p.myItems.GetRandom().Value.item,
                                        SceneManager.Instance.ExteriorPlace,
                                        randomSpawn.position,
                                        randomSpawn.rotation,
                                        false);

                                    if (worldItem)
                                    {
                                        player.svPlayer.StartGoalMarker(worldItem);
                                        return true;
                                    }
                                }
                            }
                        }
                        return false;

                    case Stage.Collecting:
                        return worldItem;

                    case Stage.Delivering:
                        foreach (var i in collectedItems)
                        {
                            if (player.MyItemCount(i.itemName.GetPrefabIndex()) < i.count)
                            {
                                player.svPlayer.SendGameMessage("Retrieval item lost..");
                                return false;
                            }
                        }
                        return true;
                }
            }
            return false;
        }

        override public void ResetTarget()
        {
            player.svPlayer.DestroyGoalMarker();

            if (worldItem)
            {
                if (targetPlayer && !targetPlayer.isHuman) worldItem.Destroy();

                worldItem = null;
            }

            if (collectedItems != null)
            {
                foreach (var i in collectedItems)
                {
                    player.TransferItem(DeltaInv.RemoveFromMe, i.itemName.GetPrefabIndex(), i.count);
                }

                collectedItems = null;
            }

            player.svPlayer.DestroyText();
            stage = Stage.NotSet;
            base.ResetTarget();
        }

        protected override void FoundTarget(bool startGoalMarker)
        {
            base.FoundTarget(startGoalMarker);
            stage = Stage.Collecting;
            player.svPlayer.SendGameMessage($"Retrieval target: {worldItem.name} for {targetPlayer.username}");
        }

        public override void Loop()
        {
            if (player.isHuman && !player.IsDead)
            {
                switch (stage)
                {
                    case Stage.NotSet:
                        SetTarget(false);
                        break;

                    case Stage.Collecting:
                        if (!ValidTarget(targetPlayer))
                        {
                            SetTarget(false);
                        }
                        else if (player.InActionRange(worldItem))
                        {
                            collectedItems = worldItem.CollectedItems;

                            foreach (var i in collectedItems)
                            {
                                player.TransferItem(DeltaInv.AddToMe, i.itemName.GetPrefabIndex(), i.count);
                            }

                            worldItem.Destroy();
                            worldItem = null;

                            stage = Stage.Delivering;

                            player.svPlayer.StartGoalMarker(targetPlayer);
                            player.svPlayer.SendGameMessage($"Reach {targetPlayer.username} in time");
                            timeDeadline = Time.time + (player.Distance(targetPlayer) * 0.1f) + 20f;
                            player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.ShowTimer, timeDeadline - Time.time);
                        }
                        break;

                    case Stage.Delivering:
                        if (!ValidTarget(targetPlayer))
                        {
                            SetTarget(false);
                        }
                        else if (Time.time > timeDeadline)
                        {
                            player.svPlayer.SendGameMessage("Out of Time");
                            SetTarget(false);
                        }
                        else if (targetPlayer.MountWithinReach(player))
                        {
                            foreach (var i in collectedItems)
                            {
                                var prefabIndex = i.itemName.GetPrefabIndex();
                                targetPlayer.TransferItem(DeltaInv.AddToMe, prefabIndex, i.count);
                                player.TransferItem(DeltaInv.RemoveFromMe, prefabIndex, i.count);
                            }

                            player.svPlayer.SendGameMessage("Item returned to owner!");
                            player.svPlayer.Reward(2, Mathf.CeilToInt(timeDeadline - Time.time));
                            SetTarget(false);
                        }
                        break;
                }
            }
        }
    }
}
