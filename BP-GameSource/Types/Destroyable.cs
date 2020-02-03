using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Destroyable : Entity
    {
        [Target(typeof(API.Events.Destroyable), (int)API.Events.Destroyable.OnDamage)]
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

        [Target(typeof(API.Events.Destroyable), (int)API.Events.Destroyable.OnDeath)]
        protected void OnDeath(ShDestroyable destroyable)
        {
        }
    }
}
