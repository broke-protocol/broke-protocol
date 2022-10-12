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

        private List<JobInfo> GetJobs => new List<JobInfo> {
            new JobInfo(
                typeof(Army), "SpecOps",
                "Kill enemy gangs to start a turf war and defeat enemy waves to capture territory",
                CharacterType.Humanoid, 0, new ColorStruct("#eb9a14"),
                new Upgrades[] {
                    new Upgrades(10,
                        new InventoryStruct[] { 
                            new InventoryStruct("Machete", 1),
                            new InventoryStruct("Flashbang", 2),
                        }),
                    new Upgrades(20,
                        new InventoryStruct[] {
                            new InventoryStruct("Smoke", 2),
                            new InventoryStruct("Colt", 1),
                            new InventoryStruct("AmmoPistol", 35),
                        }),
                    new Upgrades(30,
                        new InventoryStruct[] {
                            new InventoryStruct("KevlarVest", 1),
                            new InventoryStruct("LaserRed", 1),
                        }),
                    new Upgrades(40,
                        new InventoryStruct[] {
                            new InventoryStruct("Grenade", 2),
                            new InventoryStruct("HoloSight", 1),
                        }),
                    new Upgrades(50,
                        new InventoryStruct[] {
                            new InventoryStruct("HelmetCombat", 1),
                            new InventoryStruct("ACOG", 1),
                        }),
                    new Upgrades(60,
                        new InventoryStruct[] {
                            new InventoryStruct("AT4", 1),
                            new InventoryStruct("RocketGuided", 5),
                            new InventoryStruct("Silencer", 1),
                        })
                }),
            new JobInfo(
                typeof(Army), "OpFor",
                "Kill enemy gangs to start a turf war and defeat enemy waves to capture territory",
                CharacterType.Humanoid, 0, new ColorStruct("#3673c9"),
                new Upgrades[] {
                    new Upgrades(10,
                        new InventoryStruct[] {
                            new InventoryStruct("Machete", 1),
                            new InventoryStruct("Flashbang", 2),
                        }),
                    new Upgrades(20,
                        new InventoryStruct[] {
                            new InventoryStruct("Smoke", 2),
                            new InventoryStruct("Sig", 1),
                            new InventoryStruct("AmmoPistol", 48),
                        }),
                    new Upgrades(30,
                        new InventoryStruct[] {
                            new InventoryStruct("KevlarVest", 1),
                            new InventoryStruct("LaserRed", 1),
                        }),
                    new Upgrades(40,
                        new InventoryStruct[] {
                            new InventoryStruct("Grenade", 2),
                            new InventoryStruct("KobraSight", 1),
                        }),
                    new Upgrades(50,
                        new InventoryStruct[] {
                            new InventoryStruct("HelmetRiot", 1),
                            new InventoryStruct("ACOG", 1),
                        }),
                    new Upgrades(60,
                        new InventoryStruct[] {
                            new InventoryStruct("AT4", 1),
                            new InventoryStruct("RocketGuided", 5),
                            new InventoryStruct("Silencer", 1),
                        })
                }),
            };
    }
}
