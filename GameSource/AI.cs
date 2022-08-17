using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BrokeProtocol.GameSource.AI
{
    public class FreezeState : State
    {
        private float stopFreezeTime;

        public override void EnterState()
        {
            base.EnterState();
            stopFreezeTime = Time.time + 8f;

            player.svPlayer.SvDismount();
            player.svPlayer.SvSetEquipable(player.Surrender);
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (Time.time > stopFreezeTime && player.viewers.Count == 0)
            {
                player.svPlayer.ResetAI();
            }
        }
    }

    public class RestrainedState : State
    {
        private float stopRestrainTime;

        public override void EnterState()
        {
            base.EnterState();
            stopRestrainTime = Time.time + Random.Range(60f, 180f);
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (!player.IsRestrained)
            {
                player.svPlayer.ResetAI();
            }
            else if (Time.time >= stopRestrainTime)
            {
                player.svPlayer.SvSetEquipable(player.Hands);
                player.svPlayer.SvDismount();
                player.svPlayer.ResetAI();
            }
        }
    }

    public class WaitState : State
    {
        private float waitTime;

        public override void EnterState()
        {
            base.EnterState();
            waitTime = Time.time + 5f;
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (!player.svPlayer.IsTargetValid() || Time.time > waitTime)
            {
                player.svPlayer.ResetAI();
                return;
            }

            var target = player.svPlayer.targetEntity;

            if (player.CanSeeEntity(target) && 
                Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.SetAttackState(target);
            }
        }
    }

    public class AirAttackState : State
    {
        private ShAircraft aircraft;

        private enum AirState
        {
            Attack,
            Evade,
            Extend
        }

        public override byte StateMoveMode => MoveMode.Positive;

        public override bool EnterTest()
        {
            if (base.EnterTest() && player.svPlayer.IsTargetValid())
            {
                aircraft = player.GetControlled as ShAircraft;
                if (aircraft && aircraft.HasWeapons)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 SafePos(Vector3 movePos)
        {
            var delta = movePos - aircraft.GetPosition;
            var distance = delta.magnitude;

            var minAltitude = Mathf.Lerp(20f, 140f, distance / Util.visibleRange);

            if (Physics.SphereCast(aircraft.GetPosition, 10f, delta, out var hit, distance, MaskIndex.world))
            {
                return new Vector3(movePos.x, hit.point.y + minAltitude, movePos.z);
            }
            else
            {
                return new Vector3(movePos.x, Mathf.Max(movePos.y, minAltitude), movePos.z);
            }
        }

        private void FlySmart()
        {
            Vector3 safePos;

            player.svPlayer.SetBestWeaponSet();

            if (player.svPlayer.AimSmart())
            {
                safePos = SafePos(player.svPlayer.PredictedTarget);
                player.svPlayer.FireLogic();
            }
            else
            {
                safePos = SafePos(player.svPlayer.targetEntity.GetOrigin);
            }

            aircraft.svMountable.MoveTo(safePos);
        }

        private void FlySmart(Vector3 movePos)
        {
            var safePos = SafePos(movePos);
            aircraft.svMountable.MoveTo(safePos);
            player.svPlayer.LookAt(safePos - player.GetOrigin);
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (!player.svPlayer.IsTargetValid() || player.curMount != aircraft)
            {
                player.svPlayer.ResetAI();
                return;
            }

            var enemyMount = player.svPlayer.targetEntity.GetMount;

            if (!enemyMount)
            {
                player.svPlayer.ResetAI();
                return;
            }

            var enemyPosition = enemyMount.GetWeaponPosition();
            var enemyRotation = enemyMount.GetWeaponVector();
            const float threatLimit = 0.35f;
            var selfT = aircraft.GetRotationT;
            var selfPosition = selfT.position;
            var delta = selfPosition - enemyPosition;
            var distance = delta.magnitude;
            var direction = delta / distance;
            var dot = Vector3.Dot(-direction, selfT.forward);
            var enemyDot = Vector3.Dot(direction, enemyRotation);
            var enemyAirborn = !enemyMount.Grounded;

            AirState airState;

            if (enemyAirborn && enemyDot < 0f) // Enemy facing away and airborne
            {
                airState = AirState.Attack;
            }
            else if (dot < threatLimit) // Enemy outside front quarter
            {
                if (distance <= Util.visibleRange * 0.3f)
                {
                    // Enemy in rear quarter and aiming
                    if (enemyAirborn && dot < -threatLimit && enemyDot > 0.8f)
                        airState = AirState.Evade;
                    else
                        airState = AirState.Extend;
                }
                else airState = AirState.Attack;
            }
            else airState = AirState.Attack;

            switch (airState)
            {
                case AirState.Attack:
                    FlySmart();
                    break;
                case AirState.Evade:
                    const float offset = 100f;
                    FlySmart(selfT.TransformPoint(new Vector3(offset * (player.Perlin(0.1f) - 0.5f), offset * (player.Perlin(0.2f) - 0.5f), 1f)));
                    break;
                case AirState.Extend:
                    FlySmart(new Vector3(
                            selfPosition.x + delta.x,
                            enemyPosition.y + (aircraft.forwardVelocity * 2f - aircraft.maxSpeed),
                            selfPosition.z + delta.z));
                    break;
                default:
                    break;
            }

#if TEST
            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.LocalChatMessage, player.ID, airState.ToString());
#endif
        }
    }

    public abstract class TimedState : State
    {
        public virtual float RunTime => 1f;

        private State previousState;
        private float exitTime;

        public override bool EnterTest()
        {
            previousState = player.svPlayer.currentState;
            return base.EnterTest();
        }

        public override void EnterState()
        {
            base.EnterState();
            exitTime = Time.time + RunTime;
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (Time.time > exitTime && !player.svPlayer.SetState(previousState.index))
            {
                player.svPlayer.ResetAI();
            }
        }
    }

    public class UnstuckState : TimedState
    {
        private Vector3 previousInput;

        public override float RunTime => 1.5f;

        public override bool EnterTest()
        {
            previousInput = player.input;
            return base.EnterTest();
        }

        public override void EnterState()
        {
            player.TrySetInput(-previousInput.x, -previousInput.y, -previousInput.z);
            base.EnterState();
        }

        public override void ExitState()
        {
            base.ExitState();
            player.ZeroInputs();
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

    public abstract class MovingState : State
    {
        private bool jumped;
        private Vector3 lastCheckPosition;
        private float nextCheckTime;

        private void UpdateChecks()
        {
            var mountable = player.GetMount;
            lastCheckPosition = mountable.GetPosition;
            nextCheckTime = Time.time + 5f;
        }

        public override void EnterState()
        {
            jumped = false;
            UpdateChecks();
            base.EnterState();
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            var controlled = player.GetControlled;

            if (!player.IsPassenger && Time.time > nextCheckTime)
            {
                if (player.input.x > 0.1f && controlled.DistanceSqr(lastCheckPosition) <= controlled.maxSpeed * 0.4f)
                {
                    if (!jumped && !player.curMount)
                    {
                        player.svPlayer.SvJump();
                        jumped = true;
                    }
                    else if(SvManager.Instance.unstuck.Limit(player))
                    {
                        player.svPlayer.SvDestroySelf();
                        return;
                    }
                    else
                    {
                        player.svPlayer.SetState(Core.Unstuck.index);
                        return;
                    }
                }
                else
                {
                    jumped = false;
                }
                UpdateChecks();
            }
        }

        public override void ExitState()
        {
            base.ExitState();
            player.ZeroInputs();
        }
    }

    public class GoToState : MovingState
    {
        private bool onDestination;

        public override bool IsBusy => false;

        public override byte StateMoveMode => MoveMode.Normal;

        public override void EnterState()
        {
            base.EnterState();
            player.svPlayer.SvDismount();
            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer) && pluginPlayer.IsOffOrigin)
            {
                onDestination = false;
                player.svPlayer.GetPath(pluginPlayer.goToPosition);
            }
            else
            {
                onDestination = true;
            }
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (onDestination && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                if (pluginPlayer.IsOffOrigin)
                {
                    player.svPlayer.SetState(index);
                }
                else
                {
                    player.svPlayer.LookAt(pluginPlayer.goToRotation);
                }
            }
            else if (!player.GetControlled.svMountable.MoveLookNavPath())
            {
                player.ZeroInputs();
                onDestination = true;
            }
        }
    }

    public class WaypointState : MovingState
    {
        public override bool IsBusy => false;

        public override void EnterState()
        {
            base.EnterState();
            player.svPlayer.GetPathToWaypoints();
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            // Move on waypointPath, else navigate back to waypointPath
            if (player.svPlayer.onWaypoints)
            {
                player.GetControlled.svMountable.MoveLookWaypointPath();
            }
            else if (!player.GetControlled.svMountable.MoveLookNavPath())
            {
                player.svPlayer.onWaypoints = true;
            }
        }

        public override void ExitState()
        {
            base.ExitState();

            // Handle the case where AI are switched into cars, ResetAI is called twice
            if (!player.svPlayer.preFrame) player.svPlayer.onWaypoints = false;
        }
    }

    public class FleeState : WaypointState
    {
        private float stopFleeTime;

        public override void EnterState()
        {
            base.EnterState();
            stopFleeTime = Time.time + 10f;
        }

        public override byte StateMoveMode => MoveMode.Positive;

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (Time.time > stopFleeTime)
            {
                player.svPlayer.ResetAI();
            }
        }
    }

    public abstract class TargetState : MovingState
    {
        public override bool EnterTest() => base.EnterTest() && player.svPlayer.IsTargetValid();

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (!player.svPlayer.IsTargetValid())
            {
                player.svPlayer.ResetAI();
            }
        }
    }

    public class TakeCoverState : TargetState
    {
        private Vector3 coverOrientation;
        private bool reachedCover;
        private float waitTime;

        public override byte StateMoveMode => MoveMode.Positive;

        public override bool EnterTest()
        {
            if (base.EnterTest())
            {
                var controlled = player.GetControlled;

                var offset = controlled.bounds.size.x * 2f;

                var delta = player.svPlayer.lastTargetPosition - controlled.GetOrigin;

                var normal = delta.normalized;

                int i = 1;
                while (i < 6)
                {
                    var startPos = player.GetOrigin - (Mathf.Abs(i) * offset * normal) + Vector3.Cross(i * offset * normal, Vector3.up);

                    var currentDelta = player.svPlayer.lastTargetPosition - startPos;

                    //SvManager.Instance.DrawLine(startPos, startPos + currentDelta, Color.red, 5f);

                    if (Physics.Raycast(startPos, currentDelta, out var hit, currentDelta.magnitude, MaskIndex.hard) && Mathf.Abs(hit.normal.y) <= 0.5f)
                    {
                        var destination = hit.point + (hit.normal * controlled.bounds.extents.z);
                        if (player.svPlayer.NodeNear(destination) != null)
                        {
                            player.svPlayer.GetPath(destination);
                            reachedCover = false;
                            coverOrientation = Vector3.Cross(hit.normal, Mathf.Sign(i) * Vector3.up);
                            return true;
                        }
                    }

                    if (i > 0) i = -i;
                    else i = -i + 1;
                }
            }

            return false;
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (reachedCover)
            {
                if (player.CanSeeEntity(player.svPlayer.targetEntity) && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                {
                    pluginPlayer.SetAttackState(player.svPlayer.targetEntity);
                    return;
                }

                if (Time.time > waitTime)
                {
                    if (Random.value < 0.5f && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer2))
                    {
                        pluginPlayer2.SetAttackState(player.svPlayer.targetEntity);
                    }
                    else
                    {
                        player.svPlayer.ResetAI();
                    }
                    return;
                }

                player.svPlayer.LookAt(coverOrientation);
            }
            // Move on waypointPath, else navigate back to waypointPath
            else if (!player.GetControlled.svMountable.MoveLookNavPath())
            {
                player.ZeroInputs();
                reachedCover = true;
                waitTime = Time.time + 5f;
            }
        }
    }

    public abstract class ChaseState : TargetState
    {
        public override byte StateMoveMode => MoveMode.Normal;

        public override void EnterState()
        {
            base.EnterState();

            if (player.svPlayer.TargetNear) player.svPlayer.ResetTargetPosition();
            else player.svPlayer.PathToTarget();
        }

        protected virtual void HandleDistantTarget()
        {
            if (player.svPlayer.TargetMoved()) player.svPlayer.PathToTarget();
            else if(!player.GetControlled.svMountable.MoveLookNavPath()) player.ZeroInputs();
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (!player.svPlayer.TargetNear) HandleDistantTarget();
        }
    }

    public class FollowState : ChaseState
    {
        public override byte StateMoveMode => MoveMode.Positive;

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (player.svPlayer.TargetNear) player.ZeroInputs();

            var target = player.svPlayer.targetEntity;
            var targetMount = target.GetMount;

            if (targetMount && targetMount != target)
            {
                if (targetMount != player.curMount)
                {
                    if (player.curMount) player.svPlayer.SvDismount();
                    player.svPlayer.SvTryMount(targetMount.ID, true);
                }
            }
            else if (player.curMount)
            {
                player.svPlayer.SvDismount();
            }
        }
    }

    public abstract class AimState : ChaseState
    {
        public override void EnterState()
        {
            base.EnterState();

            if (player.IsDriving) player.svPlayer.SvSetSiren(true);
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            var targetEntity = player.svPlayer.targetEntity;

            if (player.svPlayer.TargetNear)
            {
                if (player.curMount && !player.curMount.HasWeapons && targetEntity.Velocity.sqrMagnitude <= Util.slowSpeedSqr)
                {
                    player.svPlayer.SvDismount();
                }

                player.svPlayer.AimSmart();
                var range = Mathf.Min(player.ActiveWeapon.Range + player.GetRotationT.localPosition.z, Util.pathfindRange) * 0.5f;
                player.TrySetInput(
                    Mathf.Clamp((player.Distance(targetEntity) - range) * 0.5f, -1f, 1f),
                    0f,
                    player.Perlin(0.25f) - 0.5f);
            }
        }

        public override void ExitState()
        {
            base.ExitState();

            if (player.IsDriving) player.svPlayer.SvSetSiren(false);
        }
    }

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

            if(!targetPlayer)
            {
                player.svPlayer.ResetAI();
                return;
            }

            if (threatened)
            {
                if (Time.time > stopTime)
                {
                    if(Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
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
            else if (player.svPlayer.TargetNear && Manager.pluginPlayers.TryGetValue(targetPlayer, out var pluginTarget))
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

    public class FreeState : AimState
    {
        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (player.svPlayer.TargetNear)
            {
                player.svPlayer.SvFree(player.svPlayer.targetEntity.ID);
                player.svPlayer.ResetAI();
            }
            else if (!(player.svPlayer.targetEntity is ShPlayer targetPlayer) || !targetPlayer.IsRestrained)
            {
                player.svPlayer.ResetAI();
            }
        }
    }

    public abstract class FireState : AimState
    {
        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            player.svPlayer.FireLogic();

            if (!player.svPlayer.IsTargetValid())
            {
                player.svPlayer.ResetAI();
            }
        }
    }

    public class AttackState : FireState
    {
        ShProjectile projectile;

        protected override void HandleDistantTarget()
        {
            if (player.svPlayer.TargetMoved() && 
                (player.GetPlaceIndex != player.svPlayer.targetEntity.GetPlaceIndex || 
                player.CanSeeEntity(player.svPlayer.targetEntity)))
            {
                player.svPlayer.PathToTarget();
            }
            else if (!player.GetControlled.svMountable.MoveLookNavPath())
            {
                player.svPlayer.SetState(Core.Wait.index);
            }
        }

        public override void EnterState()
        {
            base.EnterState();
            projectile = null;
            player.svPlayer.SetBestWeapon();
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (player.CanFireEquipable)
            {
                if (projectile)
                {
                    if (!player.switching)
                    {
                        player.Fire(projectile.index);
                        player.svPlayer.SetBestWeapon();
                        player.svPlayer.SetState(Core.TakeCover.index);
                    }
                }
                else
                {
                    var targetEntity = player.svPlayer.targetEntity;

                    if (targetEntity.MountHealth >= player.MountHealth && player.CanSeeEntity(targetEntity))
                    {
                        var r = Random.value;

                        if (!player.curMount && r < 0.005f && player.TryGetCachedItem(out projectile) &&
                            Util.AimVector(targetEntity.GetOrigin - player.GetOrigin, targetEntity.Velocity - player.Velocity, projectile.WeaponVelocity, projectile.WeaponGravity, out _))
                        {
                            player.svPlayer.SvTrySetEquipable(projectile.index);
                            return;
                        }
                        
                        if (r < 0.01f)
                        {
                            player.svPlayer.SetState(Core.TakeCover.index);
                            return;
                        }
                    }

                    if(player.stances[(int)StanceIndex.Crouch].input > 0f)
                        player.svPlayer.SvCrouch(player.Perlin(0.1f) < 0.3f);
                    
                    if (player.CurrentAmmoTotal == 0) player.svPlayer.SetBestWeapon();
                }
            }
        }

        public override void ExitState()
        {
            base.ExitState();
            //Don't need to reset to hands (ResetAI will do that)
            player.svPlayer.SvCrouch(false);
        }
    }

    public class ReviveState : FireState
    {
        public override void EnterState()
        {
            base.EnterState();
            player.svPlayer.SvSetEquipable(ShManager.Instance.defibrillator);
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (player.curEquipable.index != ShManager.Instance.defibrillator.index ||
                !(player.svPlayer.targetEntity is ShPlayer targetPlayer) || !targetPlayer.IsKnockedOut)
            {
                player.svPlayer.ResetAI();
            }
        }
    }

    public class HealState : FireState
    {
        public override void EnterState()
        {
            base.EnterState();
            player.svPlayer.SvSetEquipable(ShManager.Instance.healthPack);
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (player.curEquipable.index != ShManager.Instance.healthPack.index ||
                !(player.svPlayer.targetEntity is ShPlayer targetPlayer) || (targetPlayer.health >= targetPlayer.maxStat))
            {
                player.svPlayer.ResetAI();
            }
        }
    }


    public class ExtinguishState : FireState
    {
        public override void EnterState()
        {
            base.EnterState();
            player.svPlayer.SvSetEquipable(ShManager.Instance.extinguisher);
        }

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            if (player.curEquipable.index != ShManager.Instance.extinguisher.index)
            {
                player.svPlayer.ResetAI();
            }
        }
    }

    public class WanderState : MovingState
    {
        public override bool IsBusy => false;

        public override bool EnterTest() => base.EnterTest() && player.svPlayer.SelectNextNode();

        public override void UpdateState()
        {
            base.UpdateState();
            if (StateChanged) return;

            player.GetControlled.svMountable.MoveLookNodePath();
        }
    }
}