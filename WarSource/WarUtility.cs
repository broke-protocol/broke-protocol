using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using System.Collections.Generic;
using UnityEngine;

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
                place = territory.Place;

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

        private static int GetSpawnOptions(ShPlayer player, out LabelID[] options)
        {
            var optionIndex = -1;

            var optionsList = new List<LabelID>();
            var index = 0;
            foreach (var territoryIndex in GetTerritories(player.svPlayer.spawnJobIndex))
            {
                var locationName = GetTerritoryName(territoryIndex);
                optionsList.Add(new LabelID(locationName, territoryIndex.ToString()));

                if(territoryIndex == player.WarPlayer().spawnTerritoryIndex)
                {
                    optionIndex = index;
                }

                index++;
            }

            options = optionsList.ToArray();

            return optionIndex;
        }

        public static void SendSpawnMenu(ShPlayer player)
        {
            if (!player.isHuman)
                return;

            var optionIndex = GetSpawnOptions(player, out var options);

            player.svPlayer.SendTextPanel("Spawn Select:\n" + GetTerritoryName(player.WarPlayer().spawnTerritoryIndex), spawnMenuID, options, optionIndex);
        }
    }
}
