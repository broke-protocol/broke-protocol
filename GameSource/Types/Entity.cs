using BrokeProtocol.Entities;
using BrokeProtocol.API;
using BrokeProtocol.Utility;
using UnityEngine;

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
    }
}
