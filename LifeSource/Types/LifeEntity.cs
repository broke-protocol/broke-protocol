using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Utility;
using System.Linq;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class LifeEntity : EntityEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool SecurityTrigger(ShEntity entity, Collider otherCollider)
        {
            var place = entity.Place;
            if (place.owner && otherCollider.TryGetComponent(out ShPlayer player) && 
                player != place.owner && LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                place.owner.svPlayer.SendGameMessage($"{entity.name} detected {player.username} in apartment");

                if (pluginPlayer.ApartmentTrespassing(place.owner))
                    pluginPlayer.AddCrime(CrimeIndex.Trespassing, place.owner);
            }

            return true;
        }

        [Execution(ExecutionMode.PreEvent)]
        public override bool Destroy(ShEntity entity)
        {
            if (entity.GameEntity().randomSpawn)
            {
                Util.Log($"RandomSpawn Entity {entity} being destroyed", LogLevel.Warn);
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool SameSector(ShEntity e)
        {
            if (e.GameEntity().randomSpawn && e.svEntity.spectators.Count == 0 && (!e.Player || !e.Player.svPlayer.currentState.IsBusy) && !e.svEntity.sector.controlled.Any(e => e.isHuman))
            {
                e.svEntity.Deactivate(true);
            }

            return true;
        }
    }
}
