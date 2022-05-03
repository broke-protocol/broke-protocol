using BrokeProtocol.Entities;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
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
            if (destroyed is ShPlayer victim)
            {
                victim.svPlayer.SendMurderedMessage(player);
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


    

    public abstract class Army : LoopJob
    {
        public void TryFindEnemy()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && !p.IsDead && p.svPlayer.job is Army &&
                        p.svPlayer.job.info.shared.jobIndex != info.shared.jobIndex && !p.IsRestrained && player.CanSeeEntity(e),
                (e) => player.svPlayer.SetAttackState(e));
        }

        public override void Loop()
        {
            if (!player.isHuman && !player.svPlayer.targetEntity && Random.value < 0.01f && player.IsMobile && player.svPlayer.currentState.index == StateIndex.Waypoint)
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

        protected bool IsEnemyGangster(ShEntity target) => target is ShPlayer victim && this != victim.svPlayer.job;

        public override void OnDamageEntity(ShEntity damaged)
        {
            if (!IsEnemyGangster(damaged))
                base.OnDamageEntity(damaged);
        }

        public override void OnDestroyEntity(ShEntity entity)
        {
            if (IsEnemyGangster(entity))
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
            var target = player.svPlayer.spawner;

            if (target && target.IsOutside && target.svPlayer.job is Army &&
                target.svPlayer.job != this && player.DistanceSqr(target) <= Util.visibleRangeSqr)
            {
                var territory = Manager.GetTerritory(target);
                if (territory && territory.ownerIndex == info.shared.jobIndex && territory.attackerIndex != Util.invalidByte)
                {
                    if (player.svPlayer.SetAttackState(target)) return;
                }
            }
            base.ResetJobAI();
        }
    }
}
