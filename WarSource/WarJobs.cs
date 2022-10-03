using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Utility;
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

        public override void ResetJobAI()
        {
            player.svPlayer.SetBestWeapons();

            // TODO: Smarter goal selection

            if (player.IsFlying || player.IsBoating) // WaypointState if in a boat or aircraft
            {
                player.svPlayer.SetState(Core.Waypoint.index);
            }
            else
            {
                var rand = Random.value;
                if (rand < 0.2f) // Enter and hold a territory
                {
                    var territoryIndex = Random.Range(0, Manager.territories.Count);

                    if (WarUtility.GetValidTerritoryPosition(territoryIndex, out var pos, out var rot, out var place) &&
                        player.svPlayer.GetOverwatchSafe(pos, Manager.territories[territoryIndex].mainT.GetWorldBounds(), out var goal) &&
                        player.GamePlayer().SetGoToState(goal, rot, place.mTransform))
                    {

                    }
                }
                else if (rand < 0.4f) // Overwatch a territory
                {
                    var territoryIndex = Random.Range(0, Manager.territories.Count);

                    if (WarUtility.GetValidTerritoryPosition(territoryIndex, out var pos, out var rot, out var place)
                        && player.svPlayer.GetOverwatchBest(pos, out var goal) &&
                        player.GamePlayer().SetGoToState(goal, rot, place.mTransform))
                    {

                    }
                }
                else if (rand < 0.6f) // Enter a nearby vehicle
                {

                }
                else if (rand < 0.8f) // Follow a teammate
                {

                }
                else // Enter a static emplacement
                {

                }
            }


            // Nothing else to really do, maybe WanderState?
            player.svPlayer.DestroySelf();
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
                    player.GamePlayer().SetAttackState(e);
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
