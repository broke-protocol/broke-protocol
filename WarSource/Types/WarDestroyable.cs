using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class WarDestroyable : DestroyableEvents
    {
        public static bool FriendlyFire(ShDamageable damageable, ShPlayer attacker)
        {
            var controller = damageable.Controller;
            return controller && attacker && controller != attacker && attacker.svPlayer.job == controller.svPlayer.job;
        }

        // PreEvent test to disable Friendly Fire
        [Execution(ExecutionMode.PreEvent)]
        public override bool Damage(ShDamageable damageable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 source, Vector3 hitPoint) => 
            !FriendlyFire(damageable, attacker);
    }
}