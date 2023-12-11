using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class WarMovable : MovableEvents
    {
        // PreEvent test to disable Friendly Fire
        [Execution(ExecutionMode.PreEvent)]
        public override bool Damage(ShDamageable damageable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 hitPoint, Vector3 hitNormal) =>
            !WarDestroyable.FriendlyFire(damageable, attacker);

        [Execution(ExecutionMode.Override)]
        public override bool Respawn(ShEntity entity)
        {
            var player = entity.Player;
            
            if (player)
            {
                var warPlayer = player.WarPlayer();
                if (warPlayer.changePending)
                {
                    warPlayer.changePending = false;

                    if(player.svPlayer.spawnJobIndex != warPlayer.teamIndex)
                    {
                        player.svPlayer.spawnJobIndex = warPlayer.teamIndex;
                        warPlayer.cachedRank = 0;
                        player.svPlayer.SvResetJob();
                    }

                    // Remove all inventory (will be re-added either here or on spawn)
                    foreach (var i in player.myItems.ToArray())
                    {
                        player.TransferItem(DeltaInv.RemoveFromMe, i.Key, i.Value.count);
                    }
                    var newPlayer = WarManager.skinPrefabs[warPlayer.teamIndex].GetRandom();

                    // Don't try this with bots or gamemodes with Login functionality
                    // wearableIndices will be null (the list of wearables selected from the Register Menu)
                    player.svPlayer.ApplyWearableIndices(newPlayer.wearableOptions);

                    // Clamp class if it's outside the range on team change
                    warPlayer.classIndex = Mathf.Clamp(
                        warPlayer.classIndex,
                        0,
                        WarManager.classes[warPlayer.teamIndex].Count - 1);

                    foreach (var i in WarManager.classes[warPlayer.teamIndex][warPlayer.classIndex].equipment)
                    {
                        player.TransferItem(DeltaInv.AddToMe, i.itemName.GetPrefabIndex(), i.count);
                    }

                    player.svPlayer.defaultItems = null;
                }

                if (player.svPlayer.defaultItems != null && warPlayer.cachedRank != player.rank)
                {
                    for (int i = warPlayer.cachedRank + 1; i <= player.rank; i++)
                    {
                        var upgrades = player.svPlayer.job.info.shared.upgrades[i].items;

                        foreach (var item in upgrades)
                        {
                            var index = item.itemName.GetPrefabIndex();

                            if (player.svPlayer.defaultItems.TryGetValue(index, out var existing))
                            {
                                existing.count += item.count;
                            }
                            else
                            {
                                player.svPlayer.defaultItems.Add(index, new InventoryItem(SceneManager.Instance.GetEntity<ShItem>(index), item.count));
                            }
                        }
                    }

                    warPlayer.cachedRank = player.rank;
                }

                player.svPlayer.Restock(); // Will put on any suitable clothing

                if (!player.isHuman)
                {
                    // Pick a new random spawn territory for NPCs
                    warPlayer.spawnTerritoryIndex = -1;
                }

                warPlayer.SetSpawnTerritory();

                var territoryIndex = warPlayer.spawnTerritoryIndex;

                if (WarUtility.GetValidTerritoryPosition(territoryIndex, out var position, out var rotation, out var place))
                {
                    player.svEntity.originalPosition = position;
                    player.svEntity.originalRotation = rotation;
                    player.svEntity.originalParent = place.mTransform;
                }
            }

            entity.svEntity.instigator = null; // So players aren't charged with Murder crimes after vehicles reset

            entity.svEntity.SpawnOriginal();

            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool Death(ShDestroyable destroyable, ShPlayer attacker)
        {
            ShManager.Instance.StartCoroutine(DeathLoop(destroyable));
            return true;
        }

        private IEnumerator DeathLoop(ShDestroyable destroyable)
        {
            if(destroyable.Player &&
                WarManager.pluginPlayers.TryGetValue(destroyable.Player, out var warSourcePlayer))
            {
                WarUtility.SendSpawnMenu(destroyable.Player);
            }
            else
            {
                warSourcePlayer = null;
            }
            
            var respawnTime = Time.time + destroyable.svDestroyable.RespawnTime;

            while (destroyable && destroyable.IsDead)
            {
                if (warSourcePlayer != null && warSourcePlayer.SetSpawnTerritory())
                {
                    WarUtility.SendSpawnMenu(destroyable.Player);
                }

                if (Time.time >= respawnTime)
                {
                    if (warSourcePlayer == null || warSourcePlayer.spawnTerritoryIndex >= 0)
                    {
                        destroyable.svDestroyable.Respawn();
                        break;
                    }
                }

                yield return null;
            }

            if (destroyable && destroyable.Player && destroyable.isHuman)
            {
                destroyable.Player.svPlayer.DestroyTextPanel(WarUtility.spawnMenuID);
            }
        }
    }
}
