using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Utility;

namespace BrokeProtocol.GameSource.Types
{
    public class Player : Movable
    {
        [Target(GameSourceEvent.PlayerRespawn, ExecutionMode.Override)]
        public void OnRespawn(ShPlayer player)
        {
            if (player.isHuman)
            {
                var newSpawn = Manager.territories.GetRandom().transform;
                player.svPlayer.originalPosition = newSpawn.position;
                player.svPlayer.originalRotation = newSpawn.rotation;
                player.svPlayer.originalParent = newSpawn.parent;
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
