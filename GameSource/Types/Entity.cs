using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using System.Collections;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Entity : EntityEvents
    {
        

        [Execution(ExecutionMode.Additive)]
        public override bool Spawn(ShEntity entity)
        {
            var svEntity = entity.svEntity;

            if (!svEntity.respawnable)
            {
                if (svEntity.destroyAfter > 0f)
                {
                    entity.StartDestroyDelay(svEntity.destroyAfter);
                }
                else if (!entity.GetPlace.owner)
                {
                    entity.StartDestroyDelay(60f * 60f * 2f);
                }
            }
            else if (!entity.isHuman && entity.HasInventory)
            {
                entity.StartCoroutine(RestockItems(entity));
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Respawn(ShEntity entity)
        {
            entity.respawnFrame = Time.frameCount;
            if (entity.IsDead) entity.svEntity.SvDestroyEffect();
            // So players aren't charged with Murder crimes after vehicles reset
            entity.svEntity.instigator = null;

            return true;
        }

        public IEnumerator RestockItems(ShEntity entity)
        {
            var delay = new WaitForSeconds(60f);
            while (true)
            {
                yield return delay;
                entity.svEntity.Restock(0.1f);
            }
        }

        [Execution(ExecutionMode.Additive)]
        public override bool MissileLocked(ShEntity entity, ShThrown missile)
        {
            const float alertDelay = 1f;
            var controller = entity.controller;

            if (controller && Time.time - controller.GamePlayer().lastAlertTime > alertDelay)
            {
                controller.GamePlayer().lastAlertTime = Time.time;

                if (controller.isHuman)
                {
                    controller.svPlayer.SendText("&cMISSILE LOCKED", alertDelay, new Vector2(0.5f, 0.75f));
                }
                else if (controller.IsMountArmed)
                {
                    if (!controller.svPlayer.currentState.IsBusy)
                    {
                        controller.GamePlayer().SetAttackState(missile.svEntity.instigator);
                    }

                    if (Random.value < 0.4f)
                    {
                        var index = 0;

                        var mount = controller.GetMount;
                        foreach (var w in mount.weaponSets)
                        {
                            if (SceneManager.Instance.TryGetEntity<ShThrown>(w.thrownName, out var thrown) && thrown.CompareTag(ObjectTag.flareTag))
                            {
                                mount.weaponIndex = index;
                                if (mount.CanUse()) mount.MountFire();
                                controller.svPlayer.SetBestMountWeapon();
                            }

                            index++;
                        }
                    }
                }
            }

            return true;
        }
    }
}
