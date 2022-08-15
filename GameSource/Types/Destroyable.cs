using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.Utility.Networking;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Destroyable : DestroyableEvents
    {
        [Execution(ExecutionMode.Override)]
        public override bool Death(ShDestroyable destroyable, ShPlayer attacker)
        {
            if (attacker && attacker != destroyable)
            {
                attacker.svPlayer.job.OnDestroyEntity(destroyable);
            }

            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool Damage(ShDestroyable destroyable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 source, Vector3 hitPoint)
        {
            if (destroyable.IsDead) return true;

            destroyable.health -= amount;

            if (destroyable.health <= 0f)
            {
                destroyable.ShDie(attacker);
            }
            else if (attacker && attacker != destroyable)
            {
                var controller = destroyable.Controller;

                if (controller && controller != destroyable && !controller.isHuman && !controller.svPlayer.currentState.IsBusy &&
                    Manager.pluginPlayers.TryGetValue(controller, out var pluginController))
                    pluginController.SetAttackState(attacker);
                
                attacker.svPlayer.job.OnDamageEntity(destroyable);
            }

            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool DestroySelf(ShDestroyable destroyable)
        {
            if (!destroyable.IsDead)
            {
                destroyable.ShDie();
                destroyable.svDestroyable.Send(SvSendType.Local, Channel.Reliable, ClPacket.UpdateHealth, destroyable.ID, destroyable.health);
            }

            return true;
        }
    }
}
