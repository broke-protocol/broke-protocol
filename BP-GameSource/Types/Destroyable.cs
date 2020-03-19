using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.API;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Destroyable : Entity
    {
        [Target(GameSourceEvent.DestroyableDamage, ExecutionMode.Override)]
        public void OnDamage(ShDestroyable destroyable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, float hitY)
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
    }
}
