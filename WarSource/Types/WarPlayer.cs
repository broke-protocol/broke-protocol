using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class WarSourcePlayer
    {
        public ShPlayer player;

        public bool teamChangePending;
        public int spawnTerritoryIndex;
        public int teamIndex;
        public int classIndex;

        public WarSourcePlayer(ShPlayer player)
        {
            this.player = player;
        }
    }
    
    public class WarPlayer : PlayerEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Initialize(ShEntity entity)
        {
            var player = entity.Player;

            Parent.Initialize(entity);
            if (player)
            {
                var warSourcePlayer = new WarSourcePlayer(player);

                WarManager.pluginPlayers.Add(entity, warSourcePlayer);

                if (!SvManager.Instance.connections.TryGetValue(player.svPlayer.connectData.connection, out var connectData) ||
                    !connectData.customData.TryFetchCustomData(WarManager.teamIndexKey, out int teamIndex) ||
                    !connectData.customData.TryFetchCustomData(WarManager.classIndexKey, out int classIndex))
                {
                    teamIndex = player.svPlayer.spawnJobIndex;
                    classIndex = Random.Range(0, WarManager.classes[teamIndex].Count);
                }

                warSourcePlayer.teamIndex = teamIndex;
                warSourcePlayer.classIndex = classIndex;
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Destroy(ShEntity entity)
        {
            Parent.Destroy(entity);
            WarManager.pluginPlayers.Remove(entity);
            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool RemoveItemsDeath(ShPlayer player, bool dropItems) => true;

        public static IEnumerable<int> GetTerritories(int team)
        {
            var territories = new List<int>();
            var index = 0;
            foreach (var t in Manager.territories)
            {
                if (t.ownerIndex == team)
                {
                    territories.Add(index);
                }
                index++;
            }

            return territories;
        }

        public const string spawnMenuID = "SpawnMenu";

        [Execution(ExecutionMode.Additive)]
        public override bool TextPanelButton(ShPlayer player, string menuID, string optionID)
        {
            if (WarManager.pluginPlayers.TryGetValue(player, out var warSourcePlayer))
            {
                if (menuID.StartsWith(spawnMenuID))
                {
                    if (int.TryParse(optionID, out var index) && 
                        index < Manager.territories.Count && 
                        Manager.territories[index].ownerIndex == player.svPlayer.spawnJobIndex)
                    {
                        warSourcePlayer.spawnTerritoryIndex = index;
                    }
                }
            }

            return true;
        }


        [Execution(ExecutionMode.Override)]
        public override bool Respawn(ShEntity entity)
        {
            if (WarManager.pluginPlayers.TryGetValue(entity, out var warSourcePlayer))
            {
                var player = entity.Player;

                if(warSourcePlayer.teamChangePending)
                {
                    warSourcePlayer.teamChangePending = false;

                    player.svPlayer.spawnJobIndex = warSourcePlayer.teamIndex;

                    // Remove all clothing so it can be replaced with new team stuff
                    foreach(var i in player.myItems.ToArray())
                    {
                        if(i.Value.item is ShWearable)
                            player.TransferItem(DeltaInv.RemoveFromMe, i.Key, i.Value.count);
                    }

                    var newPlayer = WarManager.skinPrefabs[warSourcePlayer.teamIndex].GetRandom();

                    foreach (var options in newPlayer.wearableOptions)
                    {
                        var optionIndex = Random.Range(0, options.wearableNames.Length);
                        player.svPlayer.AddSetWearable(options.wearableNames[optionIndex].GetPrefabIndex());
                    }

                    player.svPlayer.SvSetJob(BPAPI.Jobs[warSourcePlayer.teamIndex], true, false);

                    // Clamp class if it's outside the range on team change
                    warSourcePlayer.classIndex = Mathf.Clamp(
                        warSourcePlayer.classIndex,
                        0,
                        WarManager.classes[warSourcePlayer.teamIndex].Count - 1);
                }

                player.svPlayer.AddJobItems(player.svPlayer.job.info, player.rank, false);
                player.svPlayer.defaultItems.Clear();
                foreach (var i in WarManager.classes[warSourcePlayer.teamIndex][warSourcePlayer.classIndex].equipment)
                {
                    if (SceneManager.Instance.TryGetEntity<ShItem>(i.itemName, out var item))
                    {
                        player.svPlayer.defaultItems.Add(i.itemName.GetPrefabIndex(), new InventoryItem(item, i.count));
                    }
                }

                var territoryIndex = warSourcePlayer.spawnTerritoryIndex;

                if (WarUtility.GetSpawn(territoryIndex, out var position, out var rotation, out var place))
                {
                    player.svEntity.originalPosition = position;
                    player.svEntity.originalRotation = rotation;
                    player.svEntity.originalParent = place.mTransform;
                }

                Parent.Respawn(entity);

                if (player.isHuman)
                {
                    // Back to spectate self on Respawn
                    player.svPlayer.SvSpectate(player);
                }

                player.svPlayer.Restock();
                player.svPlayer.SetBestEquipable();
            }

            return true;
        }

        // Test Event (disallow losing Exp/Job in PvP)
        [Execution(ExecutionMode.Test)]
        public override bool Reward(ShPlayer player, int experienceDelta, int moneyDelta) => 
            experienceDelta >= 0;

        [Execution(ExecutionMode.Additive)]
        public override bool OptionAction(ShPlayer player, int targetID, string id, string optionID, string actionID)
        {
            switch (id)
            {
                case WarManager.selectTeam:
                    {
                        if (WarManager.pluginPlayers.TryGetValue(player, out var warSourcePlayer))
                        {
                            int teamIndex = 0;
                            foreach (var c in BPAPI.Jobs)
                            {
                                if (c.shared.jobName == optionID)
                                {
                                    warSourcePlayer.teamIndex = teamIndex;
                                    player.svPlayer.DestroyMenu(WarManager.selectTeam);
                                    WarManager.SendClassSelectMenu(player.svPlayer.connectData.connection, teamIndex);
                                    warSourcePlayer.teamChangePending = true;
                                    break;
                                }
                                teamIndex++;
                            }
                        }
                    }
                    break;

                case WarManager.selectClass:
                    {
                        if (WarManager.pluginPlayers.TryGetValue(player, out var warSourcePlayer))
                        {
                            int classIndex = 0;
                            foreach (var c in WarManager.classes[warSourcePlayer.teamIndex])
                            {
                                if (c.className == optionID)
                                {
                                    warSourcePlayer.classIndex = classIndex;
                                    player.svPlayer.DestroyMenu(WarManager.selectClass);
                                    break;
                                }
                                classIndex++;
                            }
                        }
                    }
                    break;
            }
            return true;
        }
    }
}
