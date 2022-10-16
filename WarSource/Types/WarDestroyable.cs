using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class WarDestroyable : DestroyableEvents
    {
        public static bool FriendlyFire(ShDestroyable destroyable, ShPlayer attacker)
        {
            var controller = destroyable.Controller;
            return controller && attacker && controller != attacker && attacker.svPlayer.job == controller.svPlayer.job;
        }

        // PreEvent test to disable Friendly Fire
        [Execution(ExecutionMode.PreEvent)]
        public override bool Damage(ShDestroyable destroyable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 source, Vector3 hitPoint) => 
            !FriendlyFire(destroyable, attacker);
    }
}