using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;

namespace BrokeProtocol.GameSource.Types
{
    public class Movable : MovableEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Death(ShDestroyable destroyable, ShPlayer attacker)
        {
            if (destroyable.svDestroyable.respawnable)
            {
                // Must start coroutine on the manager because the movable will be disabled during killcam/spec mode
                ShManager.Instance.StartCoroutine(Entity.RespawnDelay(destroyable));
            }
            else
            {
                Entity.StartDestroyDelay(destroyable, destroyable.svDestroyable.RespawnTime);
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Respawn(ShEntity entity)
        {
            if (entity.svEntity.randomSpawn)
            {
                entity.svEntity.Deactivate(true);
            }
            else
            {
                var player = entity.Player;

                if (player)
                {
                    if (player.isHuman)
                    {
                        var newSpawn = Manager.spawnLocations.GetRandom().mainT;
                        player.svPlayer.originalPosition = newSpawn.position;
                        player.svPlayer.originalRotation = newSpawn.rotation;
                        player.svPlayer.originalParent = newSpawn.parent;
                    }
                    else
                    {
                        // Reset Job for NPCs so items and spawn info is correct
                        player.svPlayer.SvResetJob();
                    }
                }

                entity.svEntity.SpawnOriginal();
            }

            return true;
        }
    }
}
