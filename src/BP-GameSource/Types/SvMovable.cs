using BrokeProtocol.Entities;
using BrokeProtocol.Utility.Networking;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class SvMovable : SvDestroyable
    {
        [Target(typeof(API.Events.Movable), (int)API.Events.Movable.OnDamage)]
        protected void OnDamage(ShMovable movable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider)
        {
            base.OnDamage(movable, damageIndex, amount, attacker, collider);

            movable.svMovable.Send(SvSendType.Local,
                Channel.Reliable,
                ClPacket.UpdateHealth,
                movable.ID,
                movable.health);
        }

        [Target(typeof(API.Events.Movable), (int)API.Events.Movable.OnDeath)]
        protected void OnDeath(ShMovable movable)
        {
            if (movable.svMovable.respawnable)
            {
                movable.StartCoroutine(movable.svMovable.RespawnDelay());
            }
            else
            {
                movable.StartCoroutine(movable.DestroyDelay(movable.svMovable.GetRespawnDelay()));
            }
        }

        [Target(typeof(API.Events.Movable), (int)API.Events.Movable.OnRespawn)]
        protected void OnRespawn(ShMovable movable)
        {
            if (movable.svMovable.randomSpawn)
            {
                movable.svMovable.Despawn();
            }
            else if (movable.IsDead())
            {
                movable.svMovable.Send(SvSendType.Local, Channel.Reliable, ClPacket.Spawn,
                    movable.ID,
                    movable.originalPosition,
                    movable.originalRotation,
                    movable.svMovable.GetOriginalParentIndex());
                movable.Spawn(movable.originalPosition, movable.originalRotation, movable.originalParent);
            }
            else
            {
                movable.svMovable.ResetOriginal();
            }
        }
    }
}
