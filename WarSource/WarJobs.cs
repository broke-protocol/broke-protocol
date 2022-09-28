using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Jobs;
using System.Collections;
using UnityEngine;

namespace BrokeProtocol.GameSource
{
    public class Army : Job
    {
        public override void SetJob()
        {
            base.SetJob();
            if (player.isHuman)
            {
                foreach (var territory in Manager.territories)
                {
                    territory.svEntity.AddSubscribedPlayer(player);
                }
            }
            RestartCoroutines();
        }

        public override void RemoveJob()
        {
            if (player.isHuman)
            {
                foreach (var territory in Manager.territories)
                {
                    territory.svEntity.RemoveSubscribedPlayer(player, true);
                }
            }
            base.RemoveJob();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            RestartCoroutines();
        }

        private void RestartCoroutines()
        {
            if (player.isActiveAndEnabled && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                if (pluginPlayer.jobCoroutine != null) player.StopCoroutine(pluginPlayer.jobCoroutine);
                pluginPlayer.jobCoroutine = player.StartCoroutine(JobCoroutine());
            }
        }

        private IEnumerator JobCoroutine()
        {
            var delay = new WaitForSeconds(1f);
            do
            {
                yield return delay;
                Loop();
            } while (true);
        }

        public void Loop()
        {
            if (!player.isHuman && player.IsMobile)
            {
                TryFindEnemy();
            }
        }

        protected bool IsEnemy(ShPlayer target) => this != target.svPlayer.job;

        public override void ResetJobAI()
        {
            //Debug.Log("territories: " + Manager.territories.Count);
            var goal = Manager.territories.GetRandom();

            if (!goal)
            {
                player.svPlayer.SetState(0);
                return;
            }

            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                if (!pluginPlayer.SetGoToState(goal.mainT.position, goal.mainT.rotation, goal.mainT.parent))
                {
                    base.ResetJobAI();
                }
            }
        }

        public void TryFindEnemy()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) =>
                {
                    var p = e.Player;
                    if (p && p.IsCapable && IsEnemy(p) && player.CanSeeEntity(e, true))
                    {
                        if (!player.svPlayer.targetEntity)
                            return true;

                        return player.DistanceSqr(e) <
                        0.5f * player.DistanceSqr(player.svPlayer.targetEntity);
                    }
                    return false;
                },
                (e) =>
                {
                    if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                        pluginPlayer.SetAttackState(e);
                });
        }

        public override void OnDestroyEntity(ShEntity destroyed)
        {
            base.OnDestroyEntity(destroyed);
            var victim = destroyed.Player;
            if (victim)
            {
                if (victim.isHuman && player.isHuman)
                {
                    victim.svPlayer.SendGameMessage(player.username + " murdered " + victim.username);
                }

                if (IsEnemy(victim))
                {

                }
            }
        }
    }
}
