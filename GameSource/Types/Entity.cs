using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Jobs;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Entity : EntityEvents
    {
        [Execution(ExecutionMode.Override)]
        public override bool SecurityTrigger(ShEntity entity, Collider otherCollider)
        {
            if (entity.GetPlace is ApartmentPlace apartmentPlace && otherCollider.TryGetComponent(out ShPlayer player) && 
                player != apartmentPlace.svOwner && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                apartmentPlace.svOwner.svPlayer.SendGameMessage($"{entity.name} detected {player.username} in apartment");

                if (pluginPlayer.ApartmentTrespassing(apartmentPlace.svOwner))
                    pluginPlayer.AddCrime(CrimeIndex.Trespassing, apartmentPlace.svOwner);
            }

            return true;
        }

        public override bool Destroy(ShEntity entity)
        {
            if (entity.svEntity.randomSpawn)
            {
                var waypointIndex = (int)entity.svEntity.WaypointProperty;

                // Entity should only be part of 1 job's array but check all just in case
                foreach (var info in BPAPI.Jobs)
                {
                    ((MyJobInfo)info).randomEntities[waypointIndex].Remove(entity);
                }
            }

            return true;
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
                                controller.svPlayer.SetBestWeaponSet();
                            }

                            index++;
                        }
                    }
                }
            }

            return true;
        }

        public override bool SameSector(ShEntity e)
        {
            if (e.svEntity.randomSpawn && e.svEntity.spectators.Count == 0 && (!e.Player || !e.Player.svPlayer.currentState.IsBusy) && !e.svEntity.sector.HumanControlled())
            {
                e.svEntity.Despawn(true);
            }

            return true;
        }
    }
}
