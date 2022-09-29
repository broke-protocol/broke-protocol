using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using System.Collections;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Movable : MovableEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Damage(ShDestroyable destroyable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 source, Vector3 hitPoint)
        {
            destroyable.svDestroyable.UpdateHealth(source, hitPoint);
            return true;
        }

        private IEnumerator RespawnDelay(ShDestroyable destroyable)
        {
            var respawnTime = Time.time + destroyable.svDestroyable.RespawnTime;
            var delay = new WaitForSeconds(1f);

            while (destroyable && destroyable.IsDead)
            {
                if (Time.time >= respawnTime)
                {
                    destroyable.svDestroyable.DestroyEffect();
                    destroyable.svDestroyable.Respawn();
                    yield break;
                }
                yield return delay;
            }
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Death(ShDestroyable destroyable, ShPlayer attacker)
        {
            if (destroyable.svDestroyable.respawnable)
            {
                // Must start coroutine on the manager because the movable will be disabled during killcam/spec mode
                ShManager.Instance.StartCoroutine(RespawnDelay(destroyable));
            }
            else
            {
                destroyable.svDestroyable.StartDestroyDelay(destroyable.svDestroyable.RespawnTime);
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Respawn(ShEntity entity)
        {
            var player = entity.Player;

            if (player && player.isHuman)
            {
                var newSpawn = Manager.spawnLocations.GetRandom().mainT;
                player.svPlayer.originalPosition = newSpawn.position;
                player.svPlayer.originalRotation = newSpawn.rotation;
                player.svPlayer.originalParent = newSpawn.parent;
            }

            if (entity.svEntity.randomSpawn)
            {
                entity.svEntity.Despawn(true);
            }
            else
            {
                entity.svEntity.SpawnOriginal();
            }

            return true;
        }
    }
}
