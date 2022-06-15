using BrokeProtocol.API;
using BrokeProtocol.Entities;

namespace BrokeProtocol.WarSource.Types
{
    public class WarPlayer : WarMovable
    {
        [Target(GameSourceEvent.PlayerSpawn, ExecutionMode.Event)]
        public void OnSpawn(ShPlayer player)
        {
            if (player.svPlayer.svManager.connections.TryGetValue(player.svPlayer.connection, out var connectData) &&
                connectData.customData.TryFetchCustomData(WarManager.teamIndexKey, out int teamIndex) &&
                connectData.customData.TryFetchCustomData(WarManager.classIndexKey, out int classIndex))
            {
                player.svPlayer.SvSetJob(BPAPI.Jobs[teamIndex], true, false);
            }

            player.svPlayer.SvForceEquipable(player.svPlayer.GetBestWeapon().index);
        }

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
        }
    }
}
