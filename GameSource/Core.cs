using BrokeProtocol.API;
using BrokeProtocol.Utility.AI;
using System.Collections.Generic;

namespace BrokeProtocol.GameSource
{
    public class Core : Plugin
    {
        public static State Null;
        public static State Look;
        public static State Freeze;
        public static State Restrained;
        public static State Wait;
        public static State StaticAttack;
        public static State AirAttack;
        public static State Unstuck;
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

            Null = new BaseState();
            Look = new LookState();
            Freeze = new FreezeState();
            Restrained = new RestrainedState();
            Wait = new WaitState();
            StaticAttack = new StaticAttackState();
            AirAttack = new AirAttackState();
            Unstuck = new UnstuckState();
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
                Look,
                Freeze,
                Restrained,
                Wait,
                StaticAttack,
                AirAttack,
                Unstuck,
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
