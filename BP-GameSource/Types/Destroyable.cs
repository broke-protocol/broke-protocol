using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.API;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Destroyable : Entity
    {
        [Target(GameSourceEvent.DestroyableUpdate)]
        protected void OnUpdate(ShDestroyable destroyable)
        {
            base.OnUpdate(destroyable);
        }

        [Target(GameSourceEvent.DestroyableFixedUpdate)]
        protected void OnFixedUpdate(ShDestroyable destroyable)
        {
            base.OnFixedUpdate(destroyable);
        }

        [Target(GameSourceEvent.DestroyableDamage)]
        protected void OnDamage(ShDestroyable destroyable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider)
        {
            // Store for usage in OnDeath
            destroyable.svDestroyable.lastAttacker = attacker;

            if (destroyable.IsDead)
            {
                return;
            }

            destroyable.health -= amount;

            if (destroyable.health <= 0f)
            {
                destroyable.ShDie();
            }
        }

        [Target(GameSourceEvent.DestroyableDeath)]
        protected void OnDeath(ShDestroyable destroyable)
        {
        }
    }
}
