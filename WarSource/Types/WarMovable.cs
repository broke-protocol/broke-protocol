using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class WarMovable : MovableEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Respawn(ShEntity entity)
        {
            var player = entity.Player;

            if (player && WarManager.pluginPlayers.TryGetValue(player, out var warSourcePlayer))
            {
                if (warSourcePlayer.teamChangePending)
                {
                    warSourcePlayer.teamChangePending = false;

                    player.svPlayer.spawnJobIndex = warSourcePlayer.teamIndex;

                    // Remove all clothing so it can be replaced with new team stuff
                    foreach (var i in player.myItems.ToArray())
                    {
                        if (i.Value.item is ShWearable)
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
            }
            

            entity.svEntity.instigator = null; // So players aren't charged with Murder crimes after vehicles reset
            if (entity.IsDead)
            {
                entity.svEntity.SpawnOriginal();
            }
            else
            {
                entity.svEntity.ResetOriginal();
            }

            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool Death(ShDestroyable destroyable, ShPlayer attacker)
        {
            ShManager.Instance.StartCoroutine(DeathLoop(destroyable));
            return true;
        }

        private LabelID[] GetSpawnOptions(ShPlayer player)
        {
            var options = new List<LabelID>();
            foreach (var territoryIndex in WarPlayer.GetTerritories(player.svPlayer.spawnJobIndex))
            {
                var locationName = WarUtility.GetTerritoryName(territoryIndex);

                options.Add(new LabelID(locationName, territoryIndex.ToString()));
            }

            return options.ToArray();
        }

        private bool SetSpawnTerritory(WarSourcePlayer warPlayer)
        {
            var curSpawnIndex = warPlayer.spawnTerritoryIndex;

            if (curSpawnIndex < 0 ||
                Manager.territories[curSpawnIndex].ownerIndex != warPlayer.player.svPlayer.spawnJobIndex)
            {
                var territories = WarPlayer.GetTerritories(warPlayer.player.svPlayer.spawnJobIndex);
                if (territories.Count() > 0)
                    warPlayer.spawnTerritoryIndex = territories.GetRandom();
                else
                    warPlayer.spawnTerritoryIndex = -1;

                return warPlayer.spawnTerritoryIndex != curSpawnIndex;
            }
            return false;
        }

        private void SendSpawnMenu(WarSourcePlayer warPlayer)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Spawn Select");
            sb.AppendLine("Current Spawn:");
            if(warPlayer.spawnTerritoryIndex >= 0)
            {
                sb.AppendLine(WarUtility.GetTerritoryName(warPlayer.spawnTerritoryIndex));
            }
            else
            {
                sb.AppendLine("None");
            }
            warPlayer.player.svPlayer.SendTextPanel(sb.ToString(), WarPlayer.spawnMenuID, GetSpawnOptions(warPlayer.player));
        }

        private IEnumerator DeathLoop(ShDestroyable destroyable)
        {
            if(WarManager.pluginPlayers.TryGetValue(destroyable, out var warSourcePlayer))
            {
                SendSpawnMenu (warSourcePlayer);
            }
            
            var respawnTime = Time.time + destroyable.svDestroyable.RespawnTime;

            while (destroyable && destroyable.IsDead)
            {
                if (destroyable.Player && SetSpawnTerritory(warSourcePlayer))
                {
                    SendSpawnMenu(warSourcePlayer);
                }

                if (Time.time >= respawnTime)
                {
                    if (warSourcePlayer == null || warSourcePlayer.spawnTerritoryIndex >= 0)
                    {
                        destroyable.svDestroyable.Disappear();
                        destroyable.svDestroyable.Respawn();
                        break;
                    }
                }

                yield return null;
            }

            if (destroyable && destroyable.Player)
            {
                destroyable.Player.svPlayer.DestroyTextPanel(WarPlayer.spawnMenuID);
            }
        }
    }
}
