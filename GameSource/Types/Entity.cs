using BrokeProtocol.Entities;
using BrokeProtocol.API;
using BrokeProtocol.Utility;
using UnityEngine;
using BrokeProtocol.GameSource.Jobs;

namespace BrokeProtocol.GameSource.Types
{
    public class Entity : EntityEvents
    {
        [Execution(ExecutionMode.Override)]
        public override bool SecurityTrigger(ShEntity entity, Collider otherCollider)
        {
            if (entity.GetPlace is ApartmentPlace apartmentPlace && otherCollider.TryGetComponent(out ShPlayer player) && player != apartmentPlace.svOwner && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
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
    }
}
