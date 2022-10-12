using BrokeProtocol.API;
using BrokeProtocol.Utility.AI;
using System.Collections.Generic;

namespace BrokeProtocol.GameSource
{
    public class Core : Plugin
    {
        public static State Null = new BaseState();
        public static State Look = new LookState();
        public static State Freeze = new FreezeState();
        public static State Restrained = new RestrainedState();
        public static State Wait = new WaitState();
        public static State StaticAttack = new StaticAttackState();
        public static State AirAttack = new AirAttackState();
        public static State Unstuck = new UnstuckState();
        public static State GoTo = new GoToState();
        public static State Waypoint = new WaypointState();
        public static State Flee = new FleeState();
        public static State TakeCover = new TakeCoverState();
        public static State Follow = new FollowState();
        public static State Free = new FreeState();
        public static State Attack = new AttackState();
        public static State Revive = new ReviveState();
        public static State Extinguish = new ExtinguishState();
        public static State Wander = new WanderState();
        public static State Heal = new HealState();

        public Core()
        {
            Info = new PluginInfo(
                "GameSource",
                "game",
                "Base game source used by BP. May be modified.",
                "https://github.com/broke-protocol/broke-protocol");

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
