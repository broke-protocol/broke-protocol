using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;

namespace BrokeProtocol.WarSource.Types
{
    public class WarSourcePlayer
    {
        ShPlayer player;

        public WarSourcePlayer(ShPlayer player)
        {
            this.player = player;
        }
    }
    
    public class WarPlayer : PlayerEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Initialize(ShEntity entity)
        {
            Parent.Initialize(entity);
            WarManager.pluginPlayers.Add(entity, new WarSourcePlayer(entity as ShPlayer));
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Destroy(ShEntity entity)
        {
            Parent.Destroy(entity);
            WarManager.pluginPlayers.Remove(entity);
            return true;
        }

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

        // Test Event (disallow losing Exp/Job in PvP)
        [Execution(ExecutionMode.Test)]
        public override bool Reward(ShPlayer player, int experienceDelta, int moneyDelta) => 
            experienceDelta >= 0;
    }
}
