using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;

namespace BrokeProtocol.WarSource.Types
{
    public class WarPlayer : PlayerEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Spawn(ShEntity entity)
        {
            Parent.Spawn(entity);
            var player = entity as ShPlayer;
            if (SvManager.Instance.connections.TryGetValue(player.svPlayer.connection, out var connectData) &&
                connectData.customData.TryFetchCustomData(WarManager.teamIndexKey, out int teamIndex) &&
                connectData.customData.TryFetchCustomData(WarManager.classIndexKey, out int classIndex))
            {
                player.svPlayer.SvSetJob(BPAPI.Jobs[teamIndex], true, false);
            }
            player.svPlayer.SetBestEquipable();
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Respawn(ShEntity entity)
        {
            if (Utility.GetSpawn(out var position, out var rotation, out var place))
            {
                entity.svEntity.originalPosition = position;
                entity.svEntity.originalRotation = rotation;
                entity.svEntity.originalParent = place.mTransform;
            }

            Parent.Respawn(entity);

            if(entity.isHuman && entity is ShPlayer player)
            {
                // Back to spectate self on Respawn
                player.svPlayer.SvSpectate(player);
            }

            return true;
        }
    }
}
