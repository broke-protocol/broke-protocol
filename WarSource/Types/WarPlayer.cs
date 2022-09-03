﻿using BrokeProtocol.API;
using BrokeProtocol.Client.UI;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using System.Collections.Generic;

namespace BrokeProtocol.WarSource.Types
{
    public class WarSourcePlayer
    {
        public ShPlayer player;

        public int spawnTerritoryIndex;

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
            Parent.Initialize(entity);
            if (entity.Player)
            {
                WarManager.pluginPlayers.Add(entity, new WarSourcePlayer(entity.Player));
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

        [Execution(ExecutionMode.Additive)]
        public override bool Spawn(ShEntity entity)
        {
            Parent.Spawn(entity);
            var player = entity.Player;
            if (SvManager.Instance.connections.TryGetValue(player.svPlayer.connection, out var connectData) &&
                connectData.customData.TryFetchCustomData(WarManager.teamIndexKey, out int teamIndex) &&
                connectData.customData.TryFetchCustomData(WarManager.classIndexKey, out int classIndex))
            {
                player.svPlayer.SvSetJob(BPAPI.Jobs[teamIndex], true, false);

                foreach (var i in WarManager.classes[teamIndex][classIndex].equipment)
                {
                    if (SceneManager.Instance.TryGetEntity<ShItem>(i.itemName, out var item))
                    {
                        var count = i.count - player.MyItemCount(item);
                        if (i.count > 0)
                            player.TransferItem(DeltaInv.AddToMe, item, count);
                    }
                }
            }
            
            player.svPlayer.SetBestEquipable();
            return true;
        }

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
                        var teamIndex = WarManager.teams.IndexOf(optionID);
                        if (teamIndex >= 0 && teamIndex < WarManager.classes.Count)
                        {
                            player.svPlayer.connectData.customData.AddOrUpdate(WarManager.teamIndexKey, teamIndex);
                            player.svPlayer.DestroyMenu(WarManager.selectTeam);
                            WarManager.SendClassSelectMenu(player.svPlayer.connection, teamIndex);
                        }
                    }
                    break;

                case WarManager.selectClass:
                    {
                        if (player.svPlayer.connectData.customData.TryFetchCustomData(WarManager.teamIndexKey, out int teamIndex))
                        {
                            int classIndex = 0;
                            foreach (var c in WarManager.classes[teamIndex])
                            {
                                if (c.className == optionID)
                                {
                                    player.svPlayer.connectData.customData.AddOrUpdate(WarManager.classIndexKey, classIndex);
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
