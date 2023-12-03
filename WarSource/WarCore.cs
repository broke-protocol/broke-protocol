using BrokeProtocol.API;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
using BrokeProtocol.Utility.Jobs;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace BrokeProtocol.GameSource
{
    public class WarCore : Plugin
    {
        public static State Mount = new MountState();
        public static State TimedWaypoint = new TimedWaypointState();
        public static State TimedFollow = new TimedFollowState();
        public static State TimedGoTo = new TimedGoToState();
        public static State TimedWander = new TimedWanderState();
        public static State TimedLook = new TimedLookState();

        private const string jobDescription = "Kill and take enemy territories to burn down their tickets and win the battle";

        public WarCore()
        {
            Info = new PluginInfo(
                "WarSource",
                "war",
                "War Plugin for BP. May be modified.",
                "https://github.com/broke-protocol/broke-protocol");

            var jobsFilename = Info.Name + " Jobs.json";

            if (!File.Exists(jobsFilename))
            {
                File.WriteAllText(jobsFilename, JsonConvert.SerializeObject(GetJobs, Formatting.Indented));
            }

            // Use JobsAdditive if you're adding to Default jobs and not replacing them
            JobsOverride = JsonConvert.DeserializeObject<List<JobInfo>>(File.ReadAllText(jobsFilename));

            StatesAdditive = new List<State>
            {
                Mount,
                TimedWaypoint,
                TimedFollow,
                TimedGoTo,
                TimedWander,
                TimedLook,
            };
        }

        private List<JobInfo> GetJobs => new()
        {
            new JobInfo(
                typeof(Army), "SpecOps",
                jobDescription,
                CharacterType.Humanoid, 0, new ColorStruct("#eb9a14"),
                new Upgrades[] {
                    new (5,
                        new InventoryStruct[] { 
                            new ("Machete", 1),
                            new ("Flashbang", 2),
                        }),
                    new (10,
                        new InventoryStruct[] {
                            new ("Smoke", 2),
                            new ("Colt", 1),
                            new ("AmmoPistol", 35),
                        }),
                    new (15,
                        new InventoryStruct[] {
                            new ("KevlarVest", 1),
                            new ("LaserRed", 1),
                        }),
                    new (20,
                        new InventoryStruct[] {
                            new ("Grenade", 2),
                            new ("HoloSight", 1),
                        }),
                    new (25,
                        new InventoryStruct[] {
                            new ("HelmetCombat", 1),
                            new ("ACOG", 1),
                        }),
                    new (30,
                        new InventoryStruct[] {
                            new ("AT4", 1),
                            new ("RocketGuided", 5),
                            new ("Silencer", 1),
                        })
                }),
            new JobInfo(
                typeof(Army), "OpFor",
                jobDescription,
                CharacterType.Humanoid, 0, new ColorStruct("#3673c9"),
                new Upgrades[] {
                    new (5,
                        new InventoryStruct[] {
                            new ("Machete", 1),
                            new ("Flashbang", 2),
                        }),
                    new (10,
                        new InventoryStruct[] {
                            new ("Smoke", 2),
                            new ("Sig", 1),
                            new ("AmmoPistol", 48),
                        }),
                    new (15,
                        new InventoryStruct[] {
                            new ("KevlarVest", 1),
                            new ("LaserRed", 1),
                        }),
                    new (20,
                        new InventoryStruct[] {
                            new ("Grenade", 2),
                            new ("KobraSight", 1),
                        }),
                    new (25,
                        new InventoryStruct[] {
                            new ("HelmetRiot", 1),
                            new ("ACOG", 1),
                        }),
                    new (30,
                        new InventoryStruct[] {
                            new ("AT4", 1),
                            new ("RocketGuided", 5),
                            new ("Silencer", 1),
                        })
                }),
            };
    }
}
