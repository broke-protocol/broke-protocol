using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BrokeProtocol.GameSource
{
    public class RobState : AimState
    {
        private float stopTime;
        private bool threatened;

        public override void EnterState()
        {
            base.EnterState();
            threatened = false;
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            var targetPlayer = player.svPlayer.targetEntity.Player;

            if (!targetPlayer)
            {
                player.svPlayer.ResetAI();
                return false;
            }

            if (threatened)
            {
                if (Time.time > stopTime)
                {
                    if (player.PluginPlayer().SetAttackState(targetPlayer))
                        return false;
                }
                else if (targetPlayer.IsSurrendered && !targetPlayer.switching && TargetNear)
                {
                    player.otherEntity = targetPlayer;
                    foreach (var i in targetPlayer.myItems.Values.ToArray())
                    {
                        if (Random.value < 0.25f)
                        {
                            var randomCount = Random.Range(1, i.count);
                            player.TransferItem(DeltaInv.OtherToMe, i.item, randomCount);
                        }
                    }
                    player.otherEntity = null;
                    player.svPlayer.ResetAI();
                    return false;
                }
            }
            else if (TargetNear)
            {
                threatened = true;
                stopTime = Time.time + 8f;
                player.svPlayer.SvAlert();
                player.svPlayer.SvPoint(true);
                targetPlayer.LifePlayer().CommandHandsUp(player);
            }

            return true;
        }

        public override void ExitState(State nextState)
        {
            base.ExitState(nextState);

            if (player.pointing) player.svPlayer.SvPoint(false);
        }
    }

    public class PullOverState : TimedState
    {
        public override float RunTime => 3f;

        public override void EnterState()
        {
            player.TrySetInput(0f, 0f, -0.5f);
            base.EnterState();
        }

        public override void ExitState(State nextState)
        {
            base.ExitState(nextState);
            player.ZeroInputs();
        }
    }
}
