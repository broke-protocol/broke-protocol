using BrokeProtocol.Entities;

namespace BrokeProtocol.GameSource.Types
{
    public class Entity
    {
        [Target(typeof(API.Events.Entity), (int)API.Events.Entity.OnUpdate)]
        protected void OnUpdate(ShEntity entity)
        {
        }

        [Target(typeof(API.Events.Entity), (int)API.Events.Entity.OnFixedUpdate)]
        protected void OnFixedUpdate(ShEntity entity)
        {
        }

        [Target(typeof(API.Events.Entity), (int)API.Events.Entity.OnAddItem)]
        protected void OnAddItem(ShEntity entity, int itemIndex, int amount, bool dispatch)
        {
        }

        [Target(typeof(API.Events.Entity), (int)API.Events.Entity.OnRemoveItem)]
        protected void OnRemoveItem(ShEntity entity, int itemIndex, int amount, bool dispatch)
        {
        }
    }
}
