using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
using BrokeProtocol.Utility.Jobs;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BrokeProtocol.GameSource.Jobs
{
    public class LoopJob : Job
    {
        public override void OnDamageEntity(ShEntity damaged)
        {
            if (damaged is ShPlayer victim && victim.wantedLevel == 0)
            {
                if (victim.characterType == CharacterType.Mob)
                {
                    player.svPlayer.SvAddCrime(CrimeIndex.AnimalCruelty, victim);
                }
                else if (player.curEquipable is ShGun)
                {
                    player.svPlayer.SvAddCrime(CrimeIndex.ArmedAssault, victim);
                }
                else
                {
                    player.svPlayer.SvAddCrime(CrimeIndex.Assault, victim);
                }
            }
            else
            {
                base.OnDamageEntity(damaged);
            }
        }

        public override void OnDestroyEntity(ShEntity destroyed)
        {
            if (destroyed is ShPlayer victim && victim.wantedLevel == 0)
            {
                player.svPlayer.SvAddCrime(victim.characterType == CharacterType.Human ? CrimeIndex.Murder : CrimeIndex.AnimalKilling, victim);
                victim.svPlayer.SendMurderedMessage(player);
            }
        }

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

        protected bool MountWithinReach(ShEntity e)
        {
            var m = player.GetMount;
            return m.Velocity.sqrMagnitude <= Util.slowSpeedSqr && e.InActionRange(m);
        }
    }


    

    public class Gangster : LoopJob
    {
        protected int gangstersKilled;

        public void TryFindEnemyGang()
        {
            player.svPlayer.LocalEntitiesOne(
                (e) => e is ShPlayer p && !p.IsDead && p.svPlayer.job.info.shared.groupIndex == GroupIndex.Gang &&
                        p.svPlayer.job.info.shared.jobIndex != info.shared.jobIndex && !p.IsRestrained && player.CanSeeEntity(e),
                (e) => player.svPlayer.SetAttackState(e));
        }

        public override void Loop()
        {
            if (!player.isHuman && !player.svPlayer.targetEntity && Random.value < 0.01f && player.IsMobile && player.svPlayer.currentState.index == StateIndex.Waypoint)
            {
                TryFindEnemyGang();
            }
        }

        public override float GetSpawnRate()
        {
            // Use the spawner territory to calculate spawn rate (better AI defence spawning during gangwars)
            var territory = player.svPlayer.spawner.svPlayer.GetTerritory;

            if (territory && territory.ownerIndex == info.shared.jobIndex)
            {
                // Boost gangster spawn rate if territory under attack
                return (territory.attackerIndex == Util.invalidByte) ? info.spawnRate : 8f;
            }
            return 0f;
        }

        public override void SetJob()
        {
            if (player.isHuman)
            {
                gangstersKilled = 0;
                foreach (var territory in svManager.territories.Values)
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
                foreach (var territory in svManager.territories.Values)
                {
                    territory.svEntity.RemoveSubscribedPlayer(player, true);
                }
            }
            base.RemoveJob();
        }

        protected bool IsEnemyGangster(ShEntity target) => target is ShPlayer victim && victim.svPlayer.job is Gangster && this != victim.svPlayer.job;

        public override void OnDamageEntity(ShEntity damaged)
        {
            if (!IsEnemyGangster(damaged))
                base.OnDamageEntity(damaged);
        }

        public override void OnDestroyEntity(ShEntity entity)
        {
            if (IsEnemyGangster(entity))
            {
                if (!svManager.gangWar)
                {
                    if (player.isHuman)
                    {
                        ShTerritory t;
                        if (gangstersKilled >= 1 && (t = player.svPlayer.GetTerritory) && t.ownerIndex != info.shared.jobIndex)
                        {
                            t.svTerritory.StartGangWar(info.shared.jobIndex);
                            gangstersKilled = 0;
                        }
                        else
                        {
                            gangstersKilled++;
                        }

                        player.svPlayer.Reward(2, 50);
                    }
                }
                else
                {
                    var t = player.svPlayer.GetTerritory;
                    if (t && t.attackerIndex != Util.invalidByte && entity is ShPlayer victim)
                    {
                        if (victim.svPlayer.job.info.shared.jobIndex == t.ownerIndex)
                        {
                            t.svTerritory.defendersKilled++;
                            t.svTerritory.SendTerritoryStats();
                            player.svPlayer.Reward(3, 100);
                        }
                        else if (victim.svPlayer.job.info.shared.jobIndex == t.attackerIndex)
                        {
                            t.svTerritory.attackersKilled++;
                            t.svTerritory.SendTerritoryStats();
                            player.svPlayer.Reward(3, 100);
                        }
                    }
                }
            }
            else
            {
                base.OnDestroyEntity(entity);
            }
        }

        public override void ResetJobAI()
        {
            var target = player.svPlayer.spawner;

            if (target && target.IsOutside && target.svPlayer.job is Gangster &&
                target.svPlayer.job != this && player.DistanceSqr(target) <= Util.visibleRangeSqr)
            {
                ShTerritory territory = target.svPlayer.GetTerritory;
                if (territory && territory.ownerIndex == info.shared.jobIndex && territory.attackerIndex != Util.invalidByte)
                {
                    if (player.svPlayer.SetAttackState(target)) return;
                }
            }
            base.ResetJobAI();
        }
    }
}
