using BrokeProtocol.API;
using BrokeProtocol.GameSource.AI;
using BrokeProtocol.Utility.AI;
using System.Collections.Generic;

namespace BrokeProtocol.GameSource
{
    public class Core : Plugin
    {
        public static State Null;
        public static State Freeze;
        public static State Restrained;
        public static State Wait;
        public static State AirAttack;
        public static State Unstuck;
        public static State PullOver;
        public static State GoTo;
        public static State Waypoint;
        public static State Flee;
        public static State TakeCover;
        public static State Follow;
        public static State Free;
        public static State Attack;
        public static State Revive;
        public static State Extinguish;
        public static State Wander;
        public static State Heal;

        public Core()
        {
            Info = new PluginInfo(
                "GameSource",
                "game",
                "Base game source used by BP. May be modified.",
                "https://github.com/broke-protocol/broke-protocol");

            Null = new State();
            Freeze = new FreezeState();
            Restrained = new RestrainedState();
            Wait = new WaitState();
            AirAttack = new AirAttackState();
            Unstuck = new UnstuckState();
            PullOver = new PullOverState();
            GoTo = new GoToState();
            Waypoint = new WaypointState();
            Flee = new FleeState();
            TakeCover = new TakeCoverState();
            Follow = new FollowState();
            Free = new FreeState();
            Attack = new AttackState();
            Revive = new ReviveState();
            Extinguish = new ExtinguishState();
            Wander = new WanderState();
            Heal = new HealState();

            StatesOverride = new List<State>
            {
                Null,
                Freeze,
                Restrained,
                Wait,
                AirAttack,
                Unstuck,
                PullOver,
                GoTo,
                Waypoint,
                Flee,
                TakeCover,
                Follow,
                Free,
                Attack,
                Revive,
                Extinguish,
                Wander,
                Heal,
            };
        }
    }
}
