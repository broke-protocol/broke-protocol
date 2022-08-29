﻿using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using UnityEngine;
using System.Collections;

namespace BrokeProtocol.GameSource.Types
{
    public class Entity : EntityEvents
    {
        public override bool Spawn(ShEntity entity)
        {
            var svEntity = entity.svEntity;

            if (!svEntity.respawnable)
            {
                if (svEntity.destroyAfter > 0f)
                {
                    svEntity.StartDestroyDelay(svEntity.destroyAfter);
                }
                else if (!entity.InApartment)
                {
                    svEntity.StartDestroyDelay(60f * 60f * 2f);
                }
            }
            else if (!entity.isHuman && entity.HasInventory) entity.StartCoroutine(RestockItems(entity));

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

        public override bool MissileAlert(ShEntity entity, ShThrown missile)
        {
            const float alertDelay = 1f;
            var controller = entity.Controller;

            if (controller && Manager.pluginPlayers.TryGetValue(controller, out var pluginPlayer) &&
                Time.time - pluginPlayer.lastAlertTime > alertDelay)
            {
                pluginPlayer.lastAlertTime = Time.time;

                if (controller.isHuman)
                {
                    controller.svPlayer.SvShowAlert("&cMISSILE LOCKED", alertDelay);
                }
                else if (controller.IsMountArmed)
                {
                    if (!controller.svPlayer.currentState.IsBusy && Manager.pluginPlayers.TryGetValue(controller, out var pluginController))
                    {
                        pluginController.SetAttackState(missile.svEntity.instigator);
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
