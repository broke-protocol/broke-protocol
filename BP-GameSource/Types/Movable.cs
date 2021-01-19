using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.Utility.Networking;
using UnityEngine;
using System.Collections;

namespace BrokeProtocol.GameSource.Types
{
    public class Movable : Destroyable
    {
        [Target(GameSourceEvent.MovableDamage, ExecutionMode.Override)]
        public void OnDamage(ShMovable movable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, float hitY)
        {
            base.OnDamage(movable, damageIndex, amount, attacker, collider, hitY);

            movable.svMovable.Send(SvSendType.Local,
                Channel.Reliable,
                ClPacket.UpdateHealth,
                movable.ID,
                movable.health);
        }

        private IEnumerator RespawnDelay(ShMovable movable)
        {
            float respawnTime = Time.time + movable.svMovable.RespawnTime;
            WaitForSeconds delay = new WaitForSeconds(1f);

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

        [Target(GameSourceEvent.MovableDeath, ExecutionMode.Override)]
        public void OnDeath(ShMovable movable, ShPlayer attacker)
        {
            if (movable.svMovable.respawnable)
            {
                // Must start coroutine on the manager because the movable will be disabled during killcam/spec mode
                movable.manager.StartCoroutine(RespawnDelay(movable));
            }
            else
            {
                movable.StartCoroutine(movable.DestroyDelay(movable.svMovable.RespawnTime));
            }
        }

        [Target(GameSourceEvent.MovableRespawn, ExecutionMode.Override)]
        public void OnRespawn(ShMovable movable)
        {
            movable.svMovable.thrower = null; // So players aren't charged with Murder crimes after vehicles reset
            if (movable.svMovable.randomSpawn)
            {
                movable.svMovable.Despawn(true);
            }
            else if (movable.IsDead)
            {
                movable.svMovable.Send(SvSendType.Local, Channel.Reliable, ClPacket.Spawn,
                    movable.ID,
                    movable.originalPosition,
                    movable.originalRotation,
                    movable.svMovable.GetOriginalParentIndex);
                movable.Spawn(movable.originalPosition, movable.originalRotation, movable.originalParent);
            }
            else
            {
                movable.svMovable.ResetOriginal();
            }
        }
    }
}
