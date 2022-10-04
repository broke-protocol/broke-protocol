using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Utility;
using BrokeProtocol.Required;
using UnityEngine;
using BrokeProtocol.Entities;
using System.Collections.Generic;
using System.Text;
using BrokeProtocol.Managers;

namespace BrokeProtocol.GameSource
{
    public class ClassInfo
    {
        public readonly string className;
        public readonly InventoryStruct[] equipment;

        public ClassInfo(string className, InventoryStruct[] equipment)
        {
            this.className = className;
            this.equipment = equipment;
        }
    }

    public static class WarUtility
    {
        public const string spawnMenuID = "SpawnMenu";

        public static WarSourcePlayer WarPlayer(this ShPlayer player) => WarManager.pluginPlayers[player];

        public static IEnumerable<int> GetTerritories(int team, bool enemy = false)
        {
            var territories = new List<int>();
            var index = 0;
            foreach (var t in Manager.territories)
            {
                if (enemy ^ t.ownerIndex == team)
                {
                    territories.Add(index);
                }
                index++;
            }

            return territories;
        }

        public static bool GetValidTerritoryPosition(int territoryIndex, out Vector3 position, out Quaternion rotation, out Place place)
        {
            var territory = Manager.territories[territoryIndex];
            if (territory)
            {
                var t = territory.mainT;
                const float offset = 0.5f;
                place = territory.GetPlace;

                for (var i = 0; i < 10; i++)
                {
                    var localPosition = new Vector3(Random.value - offset, 0f, Random.value - offset);
                    
                    // Raycast down from far above and make sure hit location is vertically near territory
                    if(t.TransformPoint(localPosition).SafePosition(out var hit, 128f) && Mathf.Abs(hit.point.y - t.position.y) <= 3f)
                    {
                        position = hit.point;
                        rotation = (-position).SafeLookRotation(Vector3.up);
                        return true;
                    }
                }

                position = t.position;
                rotation = t.rotation;
                return true;
            }

            position = default;
            rotation = default;
            place = SceneManager.Instance.ExteriorPlace;
            return false;
        }

        public static string GetTerritoryName(int territoryIndex)
        {
            var territory = Manager.territories[territoryIndex];

            if (string.IsNullOrWhiteSpace(territory.text))
            {
                // Just show # if map designer didn't name territories
                return territoryIndex.ToString();
            }
            else
            {
                return territory.text;
            }
        }

        private static LabelID[] GetSpawnOptions(ShPlayer player)
        {
            var options = new List<LabelID>();
            foreach (var territoryIndex in GetTerritories(player.svPlayer.spawnJobIndex))
            {
                var locationName = GetTerritoryName(territoryIndex);

                options.Add(new LabelID(locationName, territoryIndex.ToString()));
            }

            return options.ToArray();
        }

        public static void SendSpawnMenu(WarSourcePlayer warPlayer)
        {
            if (!warPlayer.player.isHuman)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("Spawn Select");
            sb.AppendLine("Current Spawn:");
            if (warPlayer.spawnTerritoryIndex >= 0)
            {
                sb.AppendLine(GetTerritoryName(warPlayer.spawnTerritoryIndex));
            }
            else
            {
                sb.AppendLine("None");
            }
            warPlayer.player.svPlayer.SendTextPanel(sb.ToString(), spawnMenuID, GetSpawnOptions(warPlayer.player));
        }
    }
}
