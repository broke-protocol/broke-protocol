using BrokeProtocol.API;
using BrokeProtocol.Entities;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class LifeEntity : EntityEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool SecurityTrigger(ShEntity entity, Collider otherCollider)
        {
            var place = entity.GetPlace;
            if (place.owner && otherCollider.TryGetComponent(out ShPlayer player) && 
                player != place.owner && LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                place.owner.svPlayer.SendGameMessage($"{entity.name} detected {player.username} in apartment");

                if (pluginPlayer.ApartmentTrespassing(place.owner))
                    pluginPlayer.AddCrime(CrimeIndex.Trespassing, place.owner);
            }

            return true;
        }

        /* Shouldn't be destroying the random pool entities anyway
        [Execution(ExecutionMode.PreEvent)]
        public override bool Destroy(ShEntity entity)
        {
            var waypointIndex = (int)entity.svEntity.WaypointProperty;

            // Entity should only be part of 1 job's array but check all just in case
            foreach (var info in BPAPI.Jobs)
            {
                ((MyJobInfo)info).randomEntities[waypointIndex].Remove(entity);
            }
            return true;
        }
        */

        [Execution(ExecutionMode.Additive)]
        public override bool SameSector(ShEntity e)
        {
            if (e.GameEntity().randomSpawn && e.svEntity.spectators.Count == 0 && (!e.Player || !e.Player.svPlayer.currentState.IsBusy) && !e.svEntity.sector.HumanControlled())
            {
                e.svEntity.Deactivate(true);
            }

            return true;
        }
    }
}
