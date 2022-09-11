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
            var player = entity.Player;

            if (player.isHuman)
            {
                // Back to spectate self on Respawn
                player.svPlayer.SvSpectate(player);
            }

            player.svPlayer.Restock();
            player.svPlayer.SetBestEquipable();

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
