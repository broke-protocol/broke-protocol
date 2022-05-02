using BrokeProtocol.API;
using BrokeProtocol.Entities;

namespace BrokeProtocol.WarSource.Types
{
    public class WarPlayer : WarMovable
    {
        [Target(GameSourceEvent.PlayerRespawn, ExecutionMode.Override)]
        public void OnRespawn(ShPlayer player)
        {
            if (Utility.GetSpawn(out var position, out var rotation, out var place))
            {
                player.svPlayer.originalPosition = position;
                player.svPlayer.originalRotation = rotation;
                player.svPlayer.originalParent = place.mTransform;
            }

            base.OnRespawn(player);

            if(player.isHuman)
            {
                // Back to spectate self on Respawn
                player.svPlayer.SvSpectate(player);
            }

            player.svPlayer.SvForceEquipable(player.Hands.index);
        }
    }
}
