using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using System.Collections;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Transport : TransportEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Spawn(ShEntity entity)
        {
            if (entity is ShTransport t)
            {
                t.StartCoroutine(AbandonedCheck(t));
                t.StartCoroutine(CheckHazardDamage(t));
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Damage(ShDamageable damageable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 hitPoint, Vector3 hitNormal)
        {
            if(damageable is ShTransport transport && transport.health < -0.5f * transport.maxStat)
            {
                transport.svTransport.Respawn();
            }

            return true;
        }

        protected IEnumerator AbandonedCheck(ShTransport transport)
        {
            var timeStarted = false;
            var respawnTime = 0f;
            var delay = new WaitForSeconds(3f);

            while (true)
            {
                yield return delay;

                if (!timeStarted)
                {
                    if (transport.IsAbandoned)
                    {
                        timeStarted = true;
                        respawnTime = Time.time + 240f;
                    }
                }
                else if (!transport.IsAbandoned)
                {
                    timeStarted = false;
                }
                else if (Time.time > respawnTime && (transport.svTransport.randomSpawn || !transport.svTransport.OnOrigin))
                {
                    timeStarted = false;
                    transport.svTransport.Respawn();
                }
            }
        }

        protected IEnumerator CheckHazardDamage(ShTransport transport)
        {
            // Don't damge during the spawn phase
            var delay = new WaitForSeconds(2f);
            yield return delay;
            while (!transport.IsDead)
            {
                if (Time.time - transport.svTransport.collisionTime < 0.1f && transport.mainT.up.y < 0.3f || transport.InWater && transport is not ShBoat)
                {
                    transport.svTransport.Damage(DamageIndex.Null, transport.maxStat * 0.25f);
                }
                yield return delay;
            }
        }
    }
}
