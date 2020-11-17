using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.API;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Destroyable : Entity
    {
        //[Target(GameSourceEvent.DestroyableDeath, ExecutionMode.Override)]
        //public void OnDeath(ShDestroyable destroyable) { }

        [Target(GameSourceEvent.DestroyableDeath, ExecutionMode.Override)]
        public void OnDeath(ShDestroyable destroyable)
        {
            if (destroyable.svDestroyable.thrower && destroyable.svDestroyable.thrower != destroyable)
            {
                destroyable.svDestroyable.thrower.svPlayer.job.OnDestroyEntity(destroyable);
            }
        }

        [Target(GameSourceEvent.DestroyableDamage, ExecutionMode.Override)]
        public void OnDamage(ShDestroyable destroyable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, float hitY)
        {
            if (destroyable.IsDead) return;

            destroyable.health -= amount;

            if (destroyable.health <= 0f)
            {
                destroyable.svDestroyable.thrower = attacker;
                destroyable.ShDie();
            }
        }
    }
}
