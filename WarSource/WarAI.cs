using BrokeProtocol.Entities;
using BrokeProtocol.GameSource;
using BrokeProtocol.Utility;


namespace BrokeProtocol.WarSource
{
    public class MountState : ChaseState
    {
        public override byte StateMoveMode => MoveMode.Positive;

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (!(player.svPlayer.targetEntity is ShMountable mount) || !mount.IsAccessible(player, true) || mount.occupants[0])
            {
                player.svPlayer.ResetAI();
                return false;
            }
            
            if(player.svPlayer.SvTryMount(player.svPlayer.targetEntity.ID, true))
            {
                return false;
            }

            return true;
        }
    }
}
