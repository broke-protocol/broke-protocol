﻿using BrokeProtocol.Entities;
using BrokeProtocol.Utility;
using BrokeProtocol.GameSource;
using BrokeProtocol.Utility.Jobs;
using BrokeProtocol.GameSource.Types;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BrokeProtocol.WarSource.Jobs
{
    public class JobWar : Job
    {
        public override void OnDestroyEntity(ShEntity destroyed)
        {
            if (destroyed is ShPlayer victim && victim.isHuman && player.isHuman)
            {
                victim.svPlayer.SendGameMessage(player.username + " murdered " + victim.username);
            }
        }
    }


    public abstract class LoopJob : JobWar
    {
        public override void SetJob()
        {
            base.SetJob();
            RestartCoroutines();
        }

        public override void OnSpawn()
        {
            RestartCoroutines();
        }

        private void RestartCoroutines()
        {
            if (player.isActiveAndEnabled)
            {
                if (player.jobCoroutine != null) player.StopCoroutine(player.jobCoroutine);
                player.jobCoroutine = player.StartCoroutine(JobCoroutine());
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

        public virtual void Loop() { }
    }


    

    public class Army : LoopJob
    {
        public void TryFindEnemy()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && !p.IsDead && p.svPlayer.job is Army &&
                        p.svPlayer.spawnJobIndex != info.shared.jobIndex && !p.IsRestrained && player.CanSeeEntity(e),
                (e) =>
                {
                    if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                        pluginPlayer.SetAttackState(e);
                });
        }

        public override void Loop()
        {
            if (!player.isHuman && !player.svPlayer.targetEntity && Random.value < 0.01f && player.IsMobile && player.svPlayer.currentState.index == Core.Waypoint.index)
            {
                TryFindEnemy();
            }
        }

        public override void SetJob()
        {
            if (player.isHuman)
            {
                foreach (var territory in Manager.territories)
                {
                    territory.svEntity.AddSubscribedPlayer(player);
                }
            }
            base.SetJob();
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

        protected bool IsEnemy(ShEntity target) => target is ShPlayer victim && this != victim.svPlayer.job;

        public override void OnDamageEntity(ShEntity damaged)
        {
            if (!IsEnemy(damaged))
                base.OnDamageEntity(damaged);
        }

        public override void OnDestroyEntity(ShEntity entity)
        {
            if (IsEnemy(entity))
            {
                //
            }
            else
            {
                base.OnDestroyEntity(entity);
            }
        }

        public override void ResetJobAI()
        {
            //Debug.Log("territories: " + Manager.territories.Count);
            var goal = Manager.territories.GetRandom();

            if (!goal)
            {
                player.svPlayer.SetState(0);
                return;
            }

            if(Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                pluginPlayer.SetGoToState(goal.mainT.position, goal.mainT.rotation, goal.mainT.parent);
        }
    }
}