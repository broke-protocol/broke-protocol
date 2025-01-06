﻿using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BrokeProtocol.GameSource
{
    public class BaseState : State
    {
        public bool IsTargetValid()
        {
            if (player.IsCapable && player.svPlayer.targetEntity && player.svPlayer.targetEntity.svEntity.IsValidTarget(player))
                return true;

            player.svPlayer.targetEntity = null;
            return false;
        }

        public bool TargetNear => player.GetMount() == player.svPlayer.targetEntity.GetMount() ||
            player.DistanceSqr(player.svPlayer.targetEntity) < Util.closeRangeSqr ||
            player.CanSeeEntity(player.svPlayer.targetEntity, false, Util.pathfindRange);

        protected Vector3 SafePos(Vector3 movePos)
        {
            var position = player.GetControlled().Position;
            var delta = movePos - position;
            var distance = delta.magnitude;

            var minAltitude = Mathf.Lerp(20f, 140f, distance / Util.netVisibleRange);

            return new Vector3(
                movePos.x,
                Physics.SphereCast(position, 10f, delta, out var hit, distance, MaskIndex.world) ?
                hit.point.y + minAltitude : Mathf.Max(movePos.y, minAltitude),
                movePos.z);
        }

    }

    public class LookState : BaseState
    {
        public override bool IsBusy => false;

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;
            if (player.IsPassenger(out _))
                player.svPlayer.LookAt(player.curMountT.rotation);
            return true;
        }
    }

    public class FreezeState : BaseState
    {
        private float stopFreezeTime;

        public override void EnterState()
        {
            base.EnterState();
            stopFreezeTime = Time.time + 8f;

            player.svPlayer.SvDismount();
            player.svPlayer.SvSetEquipable(player.Surrender);
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (Time.time > stopFreezeTime && player.viewers.Count == 0)
            {
                player.svPlayer.ResetAI();
                return false;
            }

            return true;
        }
    }

    public class RestrainedState : LookState
    {
        private float stopRestrainTime;

        public override void EnterState()
        {
            base.EnterState();
            stopRestrainTime = Time.time + Random.Range(60f, 180f);
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (!player.IsRestrained)
            {
                player.svPlayer.ResetAI();
                return false;
            }
            else if (Time.time >= stopRestrainTime)
            {
                player.svPlayer.SvSetEquipable(player.Hands);
                player.svPlayer.SvDismount(true);
                return false;
            }

            return true;
        }
    }

    public class WaitState : BaseState
    {
        private float waitTime;

        public override void EnterState()
        {
            base.EnterState();
            waitTime = Time.time + 5f;
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (!IsTargetValid() || Time.time > waitTime)
            {
                player.svPlayer.ResetAI();
                return false;
            }

            var target = player.svPlayer.targetEntity;

            if (player.CanSeeEntity(target) && player.GamePlayer().SetAttackState(target))
            {
                return false;
            }

            return true;
        }
    }

    public class StaticAttackState : BaseState
    {
        public override bool EnterTest() => base.EnterTest() && IsTargetValid();

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (!player.curMount || !IsTargetValid())
            {
                player.svPlayer.ResetAI();
                return false;
            }

            var delta = player.svPlayer.targetEntity.Origin - player.curMount.Origin;

            if(Vector3.Angle(player.curMount.mainT.forward, delta) > player.curMount.viewAngleLimit)
            {
                player.svPlayer.SvDismount();
                if (!player.GamePlayer().SetAttackState(player.svPlayer.targetEntity))
                {
                    player.svPlayer.ResetAI();
                }
                return false;
            }

            if (player.CanSeeEntity(player.svPlayer.targetEntity))
            {
                if (player.svPlayer.AimSmart())
                {
                    player.svPlayer.FireLogic();
                }
            }
            else
            {
                player.svPlayer.LookAt(player.curMountT.rotation);
            }

            return true;
        }
    }


    public class AirAttackState : BaseState
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
            if (base.EnterTest() && IsTargetValid())
            {
                aircraft = player.GetControlled() as ShAircraft;
                return aircraft;
            }

            return false;
        }

        
        private void FlySmart()
        {
            Vector3 safePos;

            player.svPlayer.SetBestMountWeapon();

            if (player.svPlayer.AimSmart())
            {
                safePos = SafePos(player.svPlayer.PredictedTarget);
                player.svPlayer.FireLogic();
            }
            else
            {
                safePos = SafePos(player.svPlayer.targetEntity.Origin);
            }

            aircraft.svMountable.MoveTo(safePos);
        }

        private void FlySmart(Vector3 movePos)
        {
            var safePos = SafePos(movePos);
            aircraft.svMountable.MoveTo(safePos);
            player.svPlayer.LookAt(safePos - player.Origin);
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (!IsTargetValid() || player.curMount != aircraft)
            {
                player.svPlayer.ResetAI();
                return false;
            }

            var enemyMount = player.svPlayer.targetEntity.GetMount();

            if (!enemyMount)
            {
                player.svPlayer.ResetAI();
                return false;
            }

            var enemyPosition = enemyMount.GetWeaponPosition(player.seat);
            var enemyRotation = enemyMount.GetWeaponForward(player.seat);
            const float threatLimit = 0.35f;
            var selfT = aircraft.RotationT;
            var selfPosition = selfT.position;
            var delta = selfPosition - enemyPosition;
            var distance = delta.magnitude;
            var direction = delta / distance;
            var dot = Vector3.Dot(-direction, selfT.forward);
            var enemyDot = Vector3.Dot(direction, enemyRotation);
            var enemyAirborn = !enemyMount.GetGround();

            AirState airState;

            if (enemyAirborn && enemyDot < 0f) // Enemy facing away and airborne
            {
                airState = AirState.Attack;
            }
            else if (dot < threatLimit) // Enemy outside front quarter
            {
                if (distance <= Util.netVisibleRange * 0.3f)
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
            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.ChatLocal, player.ID, airState.ToString());
#endif

            return true;
        }
    }

    public abstract class TimedState : BaseState
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

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (Time.time > exitTime && !player.svPlayer.SetState(previousState.index))
            {
                player.svPlayer.ResetAI();
                return false;
            }

            return true;
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

        public override void ExitState(State nextState)
        {
            base.ExitState(nextState);
            player.ZeroInputs();
        }
    }

    public abstract class MovingState : BaseState
    {
        private bool jumped;
        private Vector3 lastCheckPosition;
        private float nextCheckTime;

        private void UpdateChecks()
        {
            var mountable = player.GetMount();
            lastCheckPosition = mountable.Position;
            nextCheckTime = Time.time + 5f;
        }

        public override void EnterState()
        {
            jumped = false;
            UpdateChecks();
            base.EnterState();
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            var controlled = player.GetControlled();

            if (!player.IsPassenger(out _) && Time.time > nextCheckTime)
            {
                if (Mathf.Abs(player.input.x) > 0.1f && controlled.DistanceSqr(lastCheckPosition) <= controlled.maxSpeed * 0.4f)
                {
                    if (!jumped && !player.curMount)
                    {
                        player.svPlayer.SvJump();
                        jumped = true;
                    }
                    else if(Utility.unstuck.Limit(player))
                    {
                        if (player.curMount)
                        {
                            player.svPlayer.SvDismount(true);
                        }
                        else
                        {
                            player.svPlayer.DestroySelf();
                        }
                        return false;
                    }
                    else if(player.svPlayer.SetState(Core.Unstuck.index))
                    {
                        return false;
                    }
                }
                else
                {
                    jumped = false;
                }
                UpdateChecks();
            }

            return true;
        }

        public override void ExitState(State nextState)
        {
            base.ExitState(nextState);
            player.ZeroInputs();
        }
    }

    public class GoToState : MovingState
    {
        protected bool onDestination;

        public override bool IsBusy => false;

        public override byte StateMoveMode => MoveMode.Normal;

        public override void EnterState()
        {
            base.EnterState();
            onDestination = false;
            if (!player.IsMount<ShAircraft>(out _))
            {
                player.svPlayer.GetPath(player.GamePlayer().goToPosition);
            }
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (onDestination)
            {
                if (player.GamePlayer().OnDestination())
                {
                    player.svPlayer.LookTactical(player.GamePlayer().goToRotation * Vector3.forward);
                }
                else if (player.svPlayer.SetState(index)) // Restart state
                {
                    return false;
                }
            }
            else if(player.IsMount<ShAircraft>(out var aircraft))
            {
                /*if(player.GamePlayer().OnDestination())
                {
                    player.svPlayer.SvDismount(true);
                    return false;
                }*/
                var goal = player.GamePlayer().goToPosition;
                player.svPlayer.LookAt(goal - aircraft.Position);
                aircraft.svTransport.MoveTo(SafePos(goal));
            }
            else if(player.svPlayer.BadPath)
            {
                player.svPlayer.ResetAI();
                return false;
            }
            else if (!player.svPlayer.MoveLookNavPath())
            {
                onDestination = true;
            }

            return true;
        }
    }

    public class WaypointState : MovingState
    {
        public override bool IsBusy => false;

        public override void EnterState()
        {
            player.svPlayer.GetPathToWaypoints();
            base.EnterState();
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            // Move on waypointPath, else navigate back to waypointPath
            if (player.svPlayer.onWaypoints)
            {
                player.svPlayer.MoveLookWaypointPath();
            }
            else if (player.svPlayer.BadPath)
            {
                player.svPlayer.ResetAI();
                return false;
            }
            else if (!player.svPlayer.MoveLookNavPath())
            {
                player.svPlayer.onWaypoints = true;
            }

            return true;
        }

        public override void ExitState(State nextState)
        {
            base.ExitState(nextState);
            // Logic to avoid searching for new waypoint if going from FleeState back to WaypointState
            // But to find a new one if going from Vehicle waypoints to Pedestrian waypoints for example
            if (nextState is not WaypointState || 
                (player.svPlayer.onWaypoints && 
                player.svPlayer.nextWaypoint.waypointType != player.GetControlled().svMovable.WaypointProperty))
            {
                player.svPlayer.onWaypoints = false;
            }
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

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (Time.time > stopFleeTime)
            {
                player.svPlayer.ResetAI();
                return false;
            }

            return true;
        }
    }

    public abstract class TargetState : MovingState
    {
        public override bool EnterTest() => base.EnterTest() && IsTargetValid();

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (!IsTargetValid())
            {
                player.svPlayer.ResetAI();
                return false;
            }

            return true;
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
                var controlled = player.GetControlled();

                var offset = controlled.bounds.size.x * 2f;

                var targetPosition = player.svPlayer.targetEntity.Position;

                var delta = targetPosition - controlled.Position;

                var normal = delta.normalized;

                int i = 1;
                while (i < 6)
                {
                    var startPos = controlled.Position - (Mathf.Abs(i) * offset * normal) + Vector3.Cross(i * offset * normal, Vector3.up);

                    if (Util.SafePosition(startPos, out var startHit))
                    {
                        var currentDelta = targetPosition - startHit.point;

                        startPos = startHit.point + Vector3.up * controlled.bounds.extents.y;

                        //SvManager.Instance.DrawLine(startPos, startPos + currentDelta, Color.red, 5f);

                        if (Physics.Raycast(startPos, currentDelta, out var hit, currentDelta.magnitude, MaskIndex.hard) && Mathf.Abs(hit.normal.y) <= 0.5f)
                        {
                            var destination = hit.point + (hit.normal * controlled.bounds.extents.z);
                            if (player.svPlayer.NodeNear(destination) != null)
                            {
                                player.svPlayer.GetPathAvoidance(destination);
                                reachedCover = false;
                                coverOrientation = Vector3.Cross(hit.normal, Mathf.Sign(i) * Vector3.up);
                                return true;
                            }
                        }
                    }

                    if (i > 0) i = -i;
                    else i = -i + 1;
                }
            }

            return false;
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (reachedCover)
            {
                if (player.CanSeeEntity(player.svPlayer.targetEntity) &&
                    player.GamePlayer().SetAttackState(player.svPlayer.targetEntity))
                {
                    return false;
                }

                if (Time.time > waitTime)
                {
                    if (Random.value < 0.5f)
                    {
                        player.GamePlayer().SetAttackState(player.svPlayer.targetEntity);
                    }
                    else
                    {
                        player.svPlayer.ResetAI();
                    }
                    return false;
                }

                player.svPlayer.LookAt(coverOrientation);
            }
            else if(player.svPlayer.BadPath)
            {
                player.GamePlayer().SetAttackState(player.svPlayer.targetEntity);
                return false;
            }
            else if (!player.svPlayer.MoveLookNavPath())
            {
                reachedCover = true;
                waitTime = Time.time + 5f;
            }

            return true;
        }

        public override void ExitState(State nextState)
        {
            base.ExitState(nextState);
            reachedCover = false;
        }
    }

    public abstract class ChaseState : TargetState
    {
        public Vector3 lastTargetPosition;

        public void ResetTargetPosition() => lastTargetPosition = player.svPlayer.targetEntity.Position;

        public bool TargetMoved => player.svPlayer.targetEntity.DistanceSqr(lastTargetPosition) > Util.pathfindRangeSqr;

        public virtual void PathToTarget()
        {
            var target = player.svPlayer.TargetMount;
            if (target.GetGround())
            {
                player.svPlayer.GetPathAvoidance(target.Position);
                ResetTargetPosition();
            }
            else
            {
                player.svPlayer.ResetPath();
            }
        }

        public override byte StateMoveMode => MoveMode.Normal;

        public override void EnterState()
        {
            ResetTargetPosition();
            PathToTarget();
            base.EnterState();
        }

        protected virtual bool HandleNearTarget()
        {
            player.svPlayer.LookTactical(player.svPlayer.targetEntity.Origin - player.Origin);
            return true;
        }

        protected bool HandleAirborneTarget()
        {
            if (!player.svPlayer.TargetMount.GetGround()) // Don't fall through below and get stuck in MoveLookNavPath/DestroySelf
            {
                player.svPlayer.LookTarget();
                player.ZeroInputs();
                return true;
            }
            return false;
        }

        protected virtual bool HandleDistantTarget()
        {
            if (HandleAirborneTarget()) // Don't fall through below and get stuck in MoveLookNavPath/DestroySelf
            {
                //
            }
            else if (TargetMoved)
            {
                PathToTarget();
            }
            else if (player.svPlayer.BadPath || !player.svPlayer.MoveLookNavPath())
            {
                // This is handled better in AttackState, but little we can do here
                player.svPlayer.ResetAI();
                return false;
            }
            return true;
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            return TargetNear ? HandleNearTarget() : HandleDistantTarget();
        }
    }

    public class FollowState : ChaseState
    {
        public override byte StateMoveMode => MoveMode.Positive;

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (TargetNear) player.ZeroInputs();

            var target = player.svPlayer.targetEntity;
            var targetMount = target.GetMount();

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

            return true;
        }
    }

    public abstract class AimState : ChaseState
    {
        public override void EnterState()
        {
            player.svPlayer.SvSetSiren(true);
            base.EnterState();
        }

        protected override bool HandleNearTarget()
        {
            player.svPlayer.AimSmart();
            return true;
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            var targetEntity = player.svPlayer.targetEntity;

            if (TargetNear)
            {
                if (player.curMount && !player.curMount.HasWeaponSet(player.seat) && targetEntity.Velocity.sqrMagnitude <= Utility.slowSpeedSqr)
                {
                    player.svPlayer.SvDismount();
                }

                var range = Mathf.Min(0.5f * player.ActiveWeapon.GetRange(player.seat) + player.RotationT.localPosition.z, 25f);
                player.TrySetInput(
                    Mathf.Clamp((player.Distance(targetEntity) - range) * 0.5f, -1f, 1f),
                    0f,
                    player.Perlin(0.25f) - 0.5f);
            }

            return true;
        }

        public override void ExitState(State nextState)
        {
            base.ExitState(nextState);
            player.svPlayer.SvSetSiren(false);
        }
    }

    public class FreeState : AimState
    {
        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (TargetNear)
            {
                player.svPlayer.SvFree(player.svPlayer.targetEntity.ID);
                player.svPlayer.ResetAI();
                return false;
            }
            else if (player.svPlayer.targetEntity is not ShPlayer targetPlayer || !targetPlayer.IsRestrained)
            {
                player.svPlayer.ResetAI();
                return false;
            }

            return true;
        }
    }

    public abstract class FireState : AimState
    {
        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            player.svPlayer.FireLogic();

            // Check again here because player or target might be killed in FireLogic
            if (!IsTargetValid())
            {
                player.svPlayer.ResetAI();
                return false;
            }

            return true;
        }
    }

    public class AttackState : FireState
    {
        protected bool hunting;
        protected ShProjectile projectile;
        protected ShDetonator detonator;

        // Don't do hunting behavior if in an unarmed vehicle
        public bool CanHunt => !player.curMount || player.curMount.HasWeaponSet(player.seat);

        public override void EnterState()
        {
            hunting = false;
            projectile = null;
            detonator = null;
            player.svPlayer.SetBestWeapons();
            base.EnterState();
        }

        public override void ExitState(State nextState)
        {
            base.ExitState(nextState);
            //Don't need to reset to hands (ResetAI will do that)
            player.svPlayer.SvCrouch(false);
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (player.CurrentAmmoTotal == 0)
                player.svPlayer.SetBestWeapons();

            if(player.curMount)
            {
                player.svPlayer.SvUpdateMode(StateMoveMode);
            }
            else
            {
                player.svPlayer.SvUpdateMode(player.Perlin(0.4f) < 0.4f ? MoveMode.Zoom : StateMoveMode);

                if (projectile)
                {
                    if (!player.switching)
                    {
                        if (detonator)
                        {
                            player.Fire(detonator.index);
                            player.svPlayer.SetBestWeapons();
                        }
                        else
                        {
                            player.Fire(projectile.index);

                            if (player.TryGetCachedItem(out detonator))
                            {
                                player.svPlayer.SvSetEquipable(detonator);
                                return true;
                            }
                            else
                            {
                                player.svPlayer.SetBestWeapons();
                            }
                        }

                        if (player.svPlayer.SetState(Core.TakeCover.index))
                        {
                            return false;
                        }

                        projectile = null;
                        detonator = null;
                    }
                }
                else
                {
                    var targetEntity = player.svPlayer.targetEntity;

                    if (targetEntity.MountHealth >= player.MountHealth && player.CanSeeEntity(targetEntity))
                    {
                        var r = Random.value;

                        if (!player.curMount && r < 0.005f && player.TryGetCachedItem(out projectile) &&
                            player.Distance(targetEntity) < Util.BallisticRange(projectile.GetWeaponVelocity(player.seat), projectile.GetWeaponGravity(player.seat)))
                        {
                            player.svPlayer.SvSetEquipable(projectile);
                        }
                        else if (r < 0.01f && player.svPlayer.SetState(Core.TakeCover.index))
                        {
                            return false;
                        }
                    }

                    if (player.stances[(int)StanceIndex.Crouch].input > 0f)
                        player.svPlayer.SvCrouch(player.Perlin(0.1f) < 0.35f);
                }
            }

            return true;
        }

        public override void PathToTarget()
        {
            // Cancel hunting if target moved
            if (hunting && TargetMoved)
            {
                hunting = false;
            }

            if ((hunting || player.svPlayer.IncompletePath || Random.value < 0.25f) && CanHunt)
            {
                hunting = true;
                if (player.svPlayer.GetOverwatchNear(player.svPlayer.targetEntity.Position, out var huntPosition))
                {
                    player.svPlayer.GetPathAvoidance(huntPosition);
                    ResetTargetPosition();
                    return;
                }
            }
            else
            {
                hunting = false;
            }

            base.PathToTarget();
        }

        protected override bool HandleNearTarget()
        {
            if (player.svPlayer.IncompletePath && CanHunt && !hunting)
            {
                PathToTarget();
            }
            else
            {
                base.HandleNearTarget();
                hunting = false;
            }

            return true;
        }

        protected override bool HandleDistantTarget()
        {
            if (player.svPlayer.IncompletePath && CanHunt && !hunting)
            {
                PathToTarget();
            }
            else if (HandleAirborneTarget())
            {
                //
            }
            else if (TargetMoved &&
                 (player.GetPlaceIndex() != player.svPlayer.targetEntity.GetPlaceIndex() || player.CanSeeEntity(player.svPlayer.targetEntity)))
            {
                PathToTarget();
            }
            else if (!player.svPlayer.MoveLookNavPath())
            {
                // Guard clause because MoveLookNavPath might kill the player if it's a bad path
                if(!player.IsCapable || !player.svPlayer.targetEntity)
                {
                    return false;
                }

                if (player.CanSeeEntity(player.svPlayer.targetEntity))
                {
                    PathToTarget();
                }
                else
                {
                    player.svPlayer.SetState(Core.Wait.index);
                    return false;
                }
            }

            return true;
        }
    }

    public class ReviveState : FireState
    {
        public override void EnterState()
        {
            base.EnterState();
            player.svPlayer.SvSetEquipable(ShManager.Instance.defibrillator);
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (player.curEquipable.index != ShManager.Instance.defibrillator.index ||
                player.svPlayer.targetEntity is not ShPlayer targetPlayer || !targetPlayer.IsKnockedOut)
            {
                player.svPlayer.ResetAI();
                return false;
            }

            return true;
        }
    }

    public class HealState : FireState
    {
        public override void EnterState()
        {
            base.EnterState();
            player.svPlayer.SvSetEquipable(ShManager.Instance.healthPack);
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (player.curEquipable.index != ShManager.Instance.healthPack.index ||
                player.svPlayer.targetEntity is not ShPlayer targetPlayer || (targetPlayer.health >= targetPlayer.maxStat))
            {
                player.svPlayer.ResetAI();
                return false;
            }

            return true;
        }
    }


    public class ExtinguishState : FireState
    {
        public override void EnterState()
        {
            base.EnterState();
            player.svPlayer.SvSetEquipable(ShManager.Instance.extinguisher);
        }

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            if (player.curEquipable.index != ShManager.Instance.extinguisher.index)
            {
                player.svPlayer.ResetAI();
                return false;
            }

            return true;
        }
    }

    public class WanderState : MovingState
    {
        public override bool IsBusy => false;

        public override bool EnterTest() => base.EnterTest() && player.svPlayer.SelectNextNode();

        public override bool UpdateState()
        {
            if (!base.UpdateState()) return false;

            player.svPlayer.MoveLookNodePath();
            return true;
        }
    }
}