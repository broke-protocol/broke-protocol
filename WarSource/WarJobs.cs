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
            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                var rand = Random.value;

                if (rand < 0.25f)
                {
                    var territoryIndex = Random.Range(0, Manager.territories.Count);

                    if (WarUtility.GetValidTerritoryPosition(territoryIndex, out var pos, out var rot, out var place) 
                        && pluginPlayer.SetGoToState(pos, rot, place.mTransform))
                    {
                        return;
                    }
                }
                else if(rand < 0.5f)
                {
                    var goal = Manager.territories.GetRandom();
                    if (goal && pluginPlayer.SetGoToState(goal.mainT.position, goal.mainT.rotation, goal.mainT.parent))
                    {
                        return;
                    }

                    if(player.svPlayer.GetOverwatchNear(player.svPlayer.targetEntity.GetPosition, out var stalkPosition))
                    {

                    }
                }
                else if (rand < 0.75f)
                {
                    // enter empty vehicle
                }
                else
                {
                    // follow someone
                }

            }


            Debug.LogWarning("[SVR] Job shouldn't end up here");
            if (player.svPlayer.SetState(0))
            {
                return;
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
