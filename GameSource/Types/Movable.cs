using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.Utility.Networking;
using UnityEngine;
using System.Collections;

namespace BrokeProtocol.GameSource.Types
{
    public class Movable : MovableEvents
    {
        [Execution(ExecutionMode.Override)]
        public override bool Damage(ShDestroyable destroyable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 source, Vector3 hitPoint)
        {
            base.Damage(destroyable, damageIndex, amount, attacker, collider, source, hitPoint);

            destroyable.svDestroyable.Send(SvSendType.Local,
                Channel.Reliable,
                ClPacket.UpdateHealth,
                destroyable.ID,
                destroyable.health,
                (hitPoint == default) ? 0f : destroyable.OutsideController ? destroyable.controller.GetFlatAngle(source) : destroyable.GetFlatAngle(source));

            return true;
        }

        private IEnumerator RespawnDelay(ShMovable movable)
        {
            var respawnTime = Time.time + movable.svMovable.RespawnTime;
            var delay = new WaitForSeconds(1f);

            while (movable && movable.IsDead)
            {
                if (Time.time >= respawnTime)
                {
                    movable.svMovable.Disappear();
                    movable.svMovable.Respawn();
                    yield break;
                }
                yield return delay;
            }
        }

        [Execution(ExecutionMode.Override)]
        public void OnDeath(ShMovable movable, ShPlayer attacker)
        {
            if (movable.svMovable.respawnable)
            {
                // Must start coroutine on the manager because the movable will be disabled during killcam/spec mode
                movable.manager.StartCoroutine(RespawnDelay(movable));
            }
            else
            {
                movable.svMovable.StartDestroyDelay(movable.svMovable.RespawnTime);
            }
        }

        [Execution(ExecutionMode.Override)]
        public void OnRespawn(ShMovable movable)
        {
            movable.svMovable.instigator = null; // So players aren't charged with Murder crimes after vehicles reset
            if (movable.svMovable.randomSpawn)
            {
                movable.svMovable.Despawn(true);
            }
            else if (movable.IsDead)
            {
                movable.svMovable.Send(SvSendType.Local, Channel.Reliable, ClPacket.Spawn,
                    movable.ID,
                    movable.svMovable.originalPosition,
                    movable.svMovable.originalRotation,
                    movable.svMovable.originalParent.GetSiblingIndex());
                movable.Spawn(movable.svMovable.originalPosition, movable.svMovable.originalRotation, movable.svMovable.originalParent);
            }
            else
            {
                movable.svMovable.ResetOriginal();
            }
        }
    }
}
