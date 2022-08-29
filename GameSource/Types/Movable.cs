using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility.Networking;
using UnityEngine;
using System.Collections;

namespace BrokeProtocol.GameSource.Types
{
    public class Movable : MovableEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Damage(ShDestroyable destroyable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 source, Vector3 hitPoint)
        {
            Parent.Damage(destroyable, damageIndex, amount, attacker, collider, source, hitPoint);

            destroyable.svDestroyable.Send(SvSendType.Local,
                Channel.Reliable,
                ClPacket.UpdateHealth,
                destroyable.ID,
                destroyable.health,
                (hitPoint == default) ? 0f : destroyable.OutsideController ? destroyable.controller.GetFlatAngle(source) : destroyable.GetFlatAngle(source));

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
                    destroyable.svDestroyable.Disappear();
                    destroyable.svDestroyable.Respawn();
                    yield break;
                }
                yield return delay;
            }
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Death(ShDestroyable destroyable, ShPlayer attacker)
        {
            Parent.Death(destroyable, attacker);

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
            Parent.Respawn(entity);
            if (entity.svEntity.randomSpawn)
            {
                entity.svEntity.Despawn(true);
            }
            else if (entity.IsDead)
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
    }
}
