using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BrokeProtocol.WarSource.Types
{
    public class WarMovable : MovableEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Respawn(ShEntity entity)
        {
            Parent.Respawn(entity);

            entity.svEntity.instigator = null; // So players aren't charged with Murder crimes after vehicles reset
            if (entity.IsDead)
            {
                entity.svEntity.Send(SvSendType.Local, Channel.Reliable, ClPacket.Spawn,
                    entity.ID,
                    entity.svEntity.originalPosition,
                    entity.svEntity.originalRotation,
                    entity.svEntity.originalParent.GetSiblingIndex());
                entity.Spawn(entity.svEntity.originalPosition, entity.svEntity.originalRotation, entity.svEntity.originalParent);
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
            Parent.Death(destroyable, attacker);

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
