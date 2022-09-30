using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Utility;

namespace BrokeProtocol.GameSource
{
    public class Army : LoopJob
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

        public override void Loop()
        {
            if (!player.isHuman && player.IsMobile)
            {
                TryFindEnemy();
            }
        }

        protected bool IsEnemy(ShPlayer target) => this != target.svPlayer.job;

        public override void ResetJobAI()
        {
            player.svPlayer.SetBestWeapons();

            //Debug.Log("territories: " + Manager.territories.Count);
            var goal = Manager.territories.GetRandom();

            if (!goal && player.svPlayer.SetState(0))
            {
                return;
            }

            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                if (pluginPlayer.SetGoToState(goal.mainT.position, goal.mainT.rotation, goal.mainT.parent))
                {
                    return;
                }
            }

            base.ResetJobAI();
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
                if (IsEnemy(victim))
                {
                    // Ticket burn
                    var victimIndex = victim.svPlayer.job.info.shared.jobIndex;

                    WarManager.tickets[victimIndex] -= 1f;

                    if (victim.isHuman && player.isHuman)
                    {
                        victim.svPlayer.SendGameMessage($"{player.username} killed  {victim.username}");
                    }
                }
                else if (player.isHuman)
                {
                    victim.svPlayer.SendGameMessage($"&4{player.username} team-killed  {victim.username}");
                }
            }
        }
    }
}
