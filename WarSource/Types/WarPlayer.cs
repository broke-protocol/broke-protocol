using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using System.Linq;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class WarSourcePlayer
    {
        public readonly ShPlayer player;

        public bool changePending;
        public int spawnTerritoryIndex;
        public int teamIndex;
        public int classIndex;
        public int cachedRank = -1;

        public WarSourcePlayer(ShPlayer player)
        {
            this.player = player;
        }

        public virtual bool SetSpawnTerritory()
        {
            var curSpawnIndex = spawnTerritoryIndex;

            if (curSpawnIndex < 0 ||
                Manager.territories[curSpawnIndex].ownerIndex != player.svPlayer.spawnJobIndex)
            {
                var territories = WarUtility.GetTerritories(player.svPlayer.spawnJobIndex);
                if (territories.Count() > 0)
                    spawnTerritoryIndex = territories.GetRandom();
                else
                    spawnTerritoryIndex = -1;

                return spawnTerritoryIndex != curSpawnIndex;
            }
            return false;
        }

        public virtual bool SetTimedGoToState(Vector3 position, Quaternion rotation)
        {
            var gamePlayer = player.GamePlayer();

            gamePlayer.goToPosition = position;
            gamePlayer.goToRotation = rotation;

            return player.svPlayer.SetState(WarCore.TimedGoTo.index);
        }

        public virtual bool SetTimedFollowState(ShPlayer leader)
        {
            player.svPlayer.leader = leader;
            player.svPlayer.targetEntity = leader;
            leader.svPlayer.follower = player;

            if (!player.svPlayer.SetState(WarCore.TimedFollow.index))
            {
                player.svPlayer.ClearLeader();
                return false;
            }

            return true;
        }
    }
    
    public class WarPlayer : PlayerEvents
    {
        // PreEvent test to disable Friendly Fire
        [Execution(ExecutionMode.PreEvent)]
        public override bool Damage(ShDamageable damageable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 hitPoint, Vector3 hitNormal) =>
            !WarDestroyable.FriendlyFire(damageable, attacker);

        [Execution(ExecutionMode.Override)]
        public override bool ResetAI(ShPlayer player)
        {
            var warPlayer = player.WarPlayer();

            player.svPlayer.targetEntity = null;

            if (player.IsKnockedOut && player.svPlayer.SetState(Core.Null.index)) return true;
            if (player.IsRestrained && player.svPlayer.SetState(Core.Restrained.index)) return true;

            player.svPlayer.SetBestWeapons();

            if (player.svPlayer.leader && warPlayer.SetTimedFollowState(player.svPlayer.leader)) return true;

            player.svPlayer.job.ResetJobAI();

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Initialize(ShEntity entity)
        {
            var player = entity.Player;

            var warSourcePlayer = new WarSourcePlayer(player);

            WarManager.pluginPlayers.Add(player, warSourcePlayer);

            if (!player.isHuman ||
                !SvManager.Instance.connections.TryGetValue(player.svPlayer.connection, out var connectData) ||
                !connectData.customData.TryGetValue(WarManager.teamIndexKey, out int teamIndex) ||
                !connectData.customData.TryGetValue(WarManager.classIndexKey, out int classIndex))
            {
                teamIndex = player.svPlayer.spawnJobIndex;
                classIndex = Random.Range(0, WarManager.classes[teamIndex].Count);
            }

            warSourcePlayer.teamIndex = teamIndex;
            warSourcePlayer.classIndex = classIndex;
            warSourcePlayer.cachedRank = player.rank;

            foreach (var i in WarManager.classes[warSourcePlayer.teamIndex][warSourcePlayer.classIndex].equipment)
            {
                player.TransferItem(DeltaInv.AddToMe, i.itemName.GetPrefabIndex(), i.count);
            }

            entity.Player.svPlayer.VisualTreeAssetClone("WarScore");

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Destroy(ShEntity entity)
        {
            WarManager.pluginPlayers.Remove(entity.Player);
            return true;
        }


        [Execution(ExecutionMode.Additive)]
        public override bool TextPanelButton(ShPlayer player, string menuID, string optionID)
        {
            if (menuID.StartsWith(WarUtility.spawnMenuID))
            {
                if (int.TryParse(optionID, out var index) && 
                    index < Manager.territories.Count && 
                    Manager.territories[index].ownerIndex == player.svPlayer.spawnJobIndex)
                {
                    player.WarPlayer().spawnTerritoryIndex = index;
                    WarUtility.SendSpawnMenu(player);
                }
            }

            return true;
        }

        // Override the GameSource Player Respawn event to cancel equipping Hands
        [Execution(ExecutionMode.Override)]
        public override bool Respawn(ShEntity entity) => true;

        [Execution(ExecutionMode.Additive)]
        public override bool Spawn(ShEntity entity)
        {
            entity.Player.svPlayer.SetBestEquipable();
            return true;
        }


        // Pre/Test Event (disallow losing Job in PvP)
        [Execution(ExecutionMode.PreEvent)]
        public override bool Reward(ShPlayer player, int experienceDelta, int moneyDelta) => experienceDelta >= 0;

        [Execution(ExecutionMode.Additive)]
        public override bool OptionAction(ShPlayer player, int targetID, string id, string optionID, string actionID)
        {
            switch (id)
            {
                case WarManager.selectTeam:
                    {
                        var teamIndex = 0;
                        foreach (var c in BPAPI.Jobs)
                        {
                            if (c.shared.jobName == optionID)
                            {
                                var warSourcePlayer = player.WarPlayer();
                                warSourcePlayer.teamIndex = teamIndex;
                                player.svPlayer.DestroyMenu(WarManager.selectTeam);
                                WarManager.SendClassSelectMenu(player.svPlayer.connection, teamIndex);
                                warSourcePlayer.changePending = true;
                                break;
                            }
                            teamIndex++;
                        }
                    }
                    break;

                case WarManager.selectClass:
                    {
                        var warSourcePlayer = player.WarPlayer();
                        var classIndex = 0;
                        foreach (var c in WarManager.classes[warSourcePlayer.teamIndex])
                        {
                            if (c.className == optionID)
                            {
                                warSourcePlayer.classIndex = classIndex;
                                player.svPlayer.DestroyMenu(WarManager.selectClass);
                                warSourcePlayer.changePending = true;
                                break;
                            }
                            classIndex++;
                        }
                    }
                    break;
            }
            return true;
        }


        // Override to Drop items without removing them from player
        [Execution(ExecutionMode.Override)]
        public override bool RemoveItemsDeath(ShPlayer player, bool dropItems)
        {
            if (dropItems)
            {
                var briefcase = player.svPlayer.SpawnBriefcase();

                if (briefcase)
                {
                    foreach (var invItem in player.myItems.Values)
                    {
                        if (Random.value < 0.8f)
                        {
                            var count = Mathf.CeilToInt(invItem.count * Random.Range(0.7f, 0.9f));
                            briefcase.myItems.Add(invItem.item.index, new InventoryItem(invItem.item, count));
                        }
                    }
                }
            }

            return true;
        }
    }
}
