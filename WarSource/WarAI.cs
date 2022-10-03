using BrokeProtocol.Entities;
using BrokeProtocol.Utility;
using UnityEngine;

namespace BrokeProtocol.GameSource
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


    public class TimedWaypointState : WaypointState
    {
        private float endTime;

        public override void EnterState()
        {
            base.EnterState();
            endTime = Time.time + Random.Range(10f, 60f);
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (Time.time > endTime)
            {
                player.svPlayer.ResetAI();
                return false;
            }

            return true;
        }
    }


    public class TimedGoToState : GoToState
    {
        private float endTime;

        public override void EnterState()
        {
            base.EnterState();
            endTime = 0f;
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (endTime > 0f)
            {
                if (Time.time > endTime)
                {
                    player.svPlayer.ResetAI();
                    return false;
                }
            }
            else if(onDestination)
            {
                endTime = Time.time + Random.Range(10f, 60f);
            }

            return true;
        }
    }
}
