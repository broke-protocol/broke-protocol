using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Utility.Networking;

namespace BrokeProtocol.WarSource.Types
{
    public class WarMovable : MovableEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Respawn(ShEntity entity)
        {
            Parent.Respawn(entity);

            entity.svEntity.instigator = null; // So players aren't charged with Murder crimes after vehicles reset
            if (entity.IsDead)
            {
                entity.svEntity.Send(SvSendType.Local, Channel.Reliable, ClPacket.Spawn,
                    entity.ID,
                    entity.svEntity.originalPosition,
                    entity.svEntity.originalRotation,
                    entity.svEntity.originalParent.GetSiblingIndex());
                entity.Spawn(entity.svEntity.originalPosition, entity.svEntity.originalRotation, entity.svEntity.originalParent);
            }
            else
            {
                entity.svEntity.ResetOriginal();
            }

            return true;
        }
    }
}
