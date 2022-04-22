using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Utility.Networking;

namespace BrokeProtocol.GameSource.Types
{
    public class Movable
    {
        [Target(GameSourceEvent.MovableRespawn, ExecutionMode.Override)]
        public void OnRespawn(ShMovable movable)
        {
            movable.svMovable.instigator = null; // So players aren't charged with Murder crimes after vehicles reset
            if (movable.svMovable.randomSpawn)
            {
                movable.svMovable.Despawn(true);
            }
            else if (movable.IsDead)
            {
                movable.svMovable.Send(SvSendType.Local, Channel.Reliable, ClPacket.Spawn,
                    movable.ID,
                    movable.svMovable.originalPosition,
                    movable.svMovable.originalRotation,
                    movable.svMovable.originalParent.GetSiblingIndex());
                movable.Spawn(movable.svMovable.originalPosition, movable.svMovable.originalRotation, movable.svMovable.originalParent);
            }
            else
            {
                movable.svMovable.ResetOriginal();
            }
        }
    }
}
