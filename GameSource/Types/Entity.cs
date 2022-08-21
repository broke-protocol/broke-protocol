using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Entity : EntityEvents
    {
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
