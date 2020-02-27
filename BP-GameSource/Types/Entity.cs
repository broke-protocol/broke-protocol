using BrokeProtocol.Entities;
using BrokeProtocol.API;

namespace BrokeProtocol.GameSource.Types
{
    public class Entity
    {
        [Target(GameSourceEvent.EntityUpdate)]
        protected void OnUpdate(ShEntity entity)
        {
        }

        [Target(GameSourceEvent.EntityFixedUpdate)]
        protected void OnFixedUpdate(ShEntity entity)
        {
        }

        [Target(GameSourceEvent.EntityAddItem)]
        protected void OnAddItem(ShEntity entity, int itemIndex, int amount, bool dispatch)
        {
        }

        [Target(GameSourceEvent.EntityRemoveItem)]
        protected void OnRemoveItem(ShEntity entity, int itemIndex, int amount, bool dispatch)
        {
        }
    }
}
