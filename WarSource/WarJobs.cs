using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Utility;
using System.Linq;
using System.Text;
using UnityEngine;

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

        protected bool TryFindMount()
        {
            return player.svPlayer.LocalEntitiesOne(
                (e) => e is ShMountable p && (p is ShMovable || p.HasWeapons) && p.IsAccessible(player, true) && !p.occupants[0],
                (e) =>
                {
                    player.svPlayer.targetEntity = e;
                    return player.svPlayer.SetState(WarCore.Mount.index);
                });
        }

        protected bool TryFindLeader()
        {
            return player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && !p.curMount && !p.svPlayer.follower && 
                p.svPlayer.leader != player && p.IsMobile,
                (e) => player.WarPlayer().SetTimedFollowState(e as ShPlayer));
        }

        public override void ResetJobAI()
        {
            if (player.curMount)
            {
                if (!(player.curMount is ShMovable))
                {
                    if (player.svPlayer.currentState.index != WarCore.TimedLook.index &&
                        player.svPlayer.SetState(WarCore.TimedLook.index))
                        return;
                }
                else
                {
                    if (player.IsFlying || player.IsBoating)
                    {
                        if (player.svPlayer.currentState.index != WarCore.TimedWaypoint.index &&
                            player.svPlayer.SetState(WarCore.TimedWaypoint.index))
                                return;
                    }
                    
                    if (!player.IsPassenger && AttackTerritory())
                    {
                        return;
                    }
                }

                player.svPlayer.SvDismount(true);
                return;
            }

            if (Random.value < 0.3f && TryFindMount())
            {
                return;
            }

            if (!player.svPlayer.leader && Random.value < 0.3f && TryFindLeader()) // Follow a teammate
            {
                return;
            }

            if (AttackTerritory())
            {
                return;
            }

            if(player.svPlayer.SetState(WarCore.TimedWander.index))
            {
                return;
            }

            // Should never get to this point
            player.svPlayer.DestroySelf();
        }

        public bool AttackTerritory()
        {
            var territories = WarUtility.GetTerritories(player.svPlayer.spawnJobIndex, true);
            if(territories.Count() == 0)
            {
                territories = WarUtility.GetTerritories(player.svPlayer.spawnJobIndex);

                if(territories.Count() == 0)
                {
                    return false;
                }    
            }

            var territoryIndex = territories.GetRandom();

            if (WarUtility.GetValidTerritoryPosition(territoryIndex, out var pos, out var rot, out var place))
            {
                // Overwatch a territory
                if (Random.value < 0.5f && player.svPlayer.GetOverwatchBest(pos, out var best) &&
                    player.WarPlayer().SetTimedGoToState(best, rot))
                {
                    return true;
                }
                else
                {
                    var t = Manager.territories[territoryIndex].mainT;

                    var tBounds = new Bounds(t.position, default);
                    
                    tBounds.Encapsulate(t.TransformPoint(-1f, -1f, 0f));
                    tBounds.Encapsulate(t.TransformPoint( 0f, -1f, -1f));
                    tBounds.Encapsulate(t.TransformPoint( 1f, -1f, 0f));
                    tBounds.Encapsulate(t.TransformPoint( 0f, -1f, 1f));
                    tBounds.Encapsulate(t.TransformPoint(-1f, 1f, 0f));
                    tBounds.Encapsulate(t.TransformPoint( 0f, 1f, -1f));
                    tBounds.Encapsulate(t.TransformPoint( 1f, 1f, 0f));
                    tBounds.Encapsulate(t.TransformPoint( 0f, 1f, 1f));

                    if (player.svPlayer.GetOverwatchSafe(pos, tBounds, out var safe) &&
                        player.WarPlayer().SetTimedGoToState(safe, rot))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void TryFindEnemy() => player.svPlayer.LocalEntitiesOne(
                (e) =>
                {
                    var p = e.Player;
                    if (p && p.IsCapable && IsEnemy(p) && player.CanSeeEntity(e, true))
                    {
                        // Find enemies even when following someone
                        if (!player.svPlayer.targetEntity || player.svPlayer.targetEntity == player.svPlayer.leader)
                            return true;

                        var canSeeExistingTarget = player.CanSeeEntity(player.svPlayer.targetEntity);

                        return !canSeeExistingTarget || player.DistanceSqr(e) < 0.5f * player.DistanceSqr(player.svPlayer.targetEntity);
                    }
                    return false;
                },
                (e) => player.GamePlayer().SetAttackState(e));

        public override void OnDestroyEntity(ShEntity destroyed)
        {
            base.OnDestroyEntity(destroyed);
            var victim = destroyed.Player;
            if (victim)
            {
                if (IsEnemy(victim))
                {
                    // TODO: Reward players for stuff other than killing
                    player.svPlayer.Reward(1, 0);
                    // Ticket burn
                    var victimIndex = victim.svPlayer.job.info.shared.jobIndex;

                    WarManager.tickets[victimIndex] -= 1f;

                    if (victim.isHuman && player.isHuman)
                    {
                        InterfaceHandler.SendGameMessageToAll(KillString(player, victim, " killed "));
                    }
                }
                else if (player.isHuman)
                {
                    InterfaceHandler.SendGameMessageToAll(KillString(player, victim, " &4team-killed "));
                }
            }
        }

        private string KillString(ShPlayer attacker, ShPlayer victim, string s)
        {
            var sb = new StringBuilder();
            sb.AppendColorText(attacker.username, attacker.svPlayer.job.info.shared.GetColor());
            sb.Append(s);
            sb.AppendColorText(victim.username, victim.svPlayer.job.info.shared.GetColor());

            return sb.ToString();
        }
    }
}
