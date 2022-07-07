using BrokeProtocol.Entities;
using BrokeProtocol.API;
using BrokeProtocol.Utility;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Entity
    {
        //[Target(GameSourceEvent.EntityInitialize, ExecutionMode.Override)]
        //public void OnInitialize(ShEntity entity) { }

        //[Target(GameSourceEvent.EntitySpawn, ExecutionMode.Override)]
        //public void OnSpawn(ShEntity entity) { }

        //[Target(GameSourceEvent.EntityDestroy, ExecutionMode.Override)]
        //public void OnDestroy(ShEntity entity) { }

        //[Target(GameSourceEvent.EntityAddItem, ExecutionMode.Override)]
        //public void OnAddItem(ShEntity entity, int itemIndex, int amount, bool dispatch) { }

        //[Target(GameSourceEvent.EntityRemoveItem, ExecutionMode.Override)]
        //public void OnRemoveItem(ShEntity entity, int itemIndex, int amount, bool dispatch) { }

        //[Target(GameSourceEvent.EntityRespawn, ExecutionMode.Override)]
        //public void OnRespawn(ShEntity entity) { }

        //[Target(GameSourceEvent.EntityTransferItem, ExecutionMode.Override)]
        //public void OnTransferItem(ShEntity entity, byte deltaType, int itemIndex, int amount, bool dispatch) { }

        [Target(GameSourceEvent.EntitySecurityTrigger, ExecutionMode.Override)]
        public void OnSecurityTrigger(ShEntity entity, Collider otherCollider)
        {
            if (entity.GetPlace is ApartmentPlace apartmentPlace && otherCollider.TryGetComponent(out ShPlayer player) && player != apartmentPlace.svOwner && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                apartmentPlace.svOwner.svPlayer.SendGameMessage($"{entity.name} detected {player.username} in apartment");

                if (pluginPlayer.ApartmentTrespassing(apartmentPlace.svOwner))
                    pluginPlayer.AddCrime(CrimeIndex.Trespassing, apartmentPlace.svOwner);
            }
        }
    }
}
