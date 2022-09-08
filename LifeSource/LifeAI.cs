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

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            var targetPlayer = player.svPlayer.targetEntity.Player;

            if (!targetPlayer)
            {
                player.svPlayer.ResetAI();
                return;
            }

            if (threatened)
            {
                if (Time.time > stopTime)
                {
                    if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                        pluginPlayer.SetAttackState(targetPlayer);
                }
                else if (targetPlayer.IsSurrendered && !targetPlayer.switching && player.svPlayer.TargetNear)
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
                }
            }
            else if (player.svPlayer.TargetNear && LifeManager.pluginPlayers.TryGetValue(targetPlayer, out var pluginTarget))
            {
                threatened = true;
                stopTime = Time.time + 8f;
                player.svPlayer.SvAlert();
                player.svPlayer.SvPoint(true);
                pluginTarget.CommandHandsUp(player);
            }
        }

        public override void ExitState()
        {
            base.ExitState();

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

        public override void ExitState()
        {
            base.ExitState();
            player.ZeroInputs();
        }
    }
}
