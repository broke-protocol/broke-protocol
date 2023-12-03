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
    public class LifeCore : Plugin
    {
        public static State Rob = new RobState();
        public static State PullOver = new PullOverState();

        public static int prisonerIndex = -1;
        public static int policeIndex = -1;

        public LifeCore()
        {
            Info = new PluginInfo(
                "LifeSource",
                "life",
                "Roleplay source used by BP. May be modified.",
                "https://github.com/broke-protocol/broke-protocol");

            var jobsFilename = Info.Name + " Jobs.json";

            if (!File.Exists(jobsFilename))
            {
                File.WriteAllText(jobsFilename, JsonConvert.SerializeObject(GetJobs, Formatting.Indented));
            }

            // Use JobsAdditive if you're adding to Default jobs and not replacing them
            JobsOverride = new List<JobInfo>();
            var myJobs = JsonConvert.DeserializeObject<List<MyJobInfo>>(File.ReadAllText(jobsFilename));
            foreach (var job in myJobs)
            {
                JobsOverride.Add(job);
            }

            var index = 0;
            foreach (var info in myJobs)
            {
                if (prisonerIndex < 0 && info.groupIndex == GroupIndex.Prisoner)
                {
                    prisonerIndex = index;
                }
                else if (policeIndex < 0 && info.groupIndex == GroupIndex.LawEnforcement)
                {
                    policeIndex = index;
                }
                index++;
            }

            StatesAdditive = new List<State>
            {
                Rob,
                PullOver,
            };
        }

        private List<MyJobInfo> GetJobs => new()
        {
            /*
            new MyJobInfo(
                typeof(Citizen), "Job Name",
                "Job Description",
                CharacterType.All, 0, GroupIndex.Citizen, new ColorStruct(0.75f, 0.75f, 0.75f), 1f, 28,
                new Transports[] {
                    new (new string[] {
                        "Car1",
                        "Car2" }),
                    new (new string[] {
                        "Aircraft1",
                        "Aircraft2" }),
                    new (new string[] {
                        "Boat1",
                        "Boat2" }),
                    new (new string[] {
                        "Train1",
                        "Train2" }),
                    new (new string[] {
                        "Towable1",
                        "Towable2" }),
                },
                new Upgrades[] {
                    new (10,
                        new InventoryStruct[] {
                            new ("Item1", 1),
                            new ("Item2", 1)}),
                    new (20,
                        new InventoryStruct[] {
                            new ("Item1", 1),
                            new ("Item2", 1)}),
                    new (20,
                        new InventoryStruct[] {
                            new ("Item1", 1),
                            new ("Item2", 1)})
                }),
            */

            new MyJobInfo(
                typeof(Citizen), "Citizen",
                "Get money by robbing, looting, and trading with NPCs and players or get a job by visiting map icons",
                CharacterType.All, 0, GroupIndex.Citizen, new ColorStruct(0.75f, 0.75f, 0.75f), 1f, 28,
                new Transports[] {
                    new (new string[] {
                        "Car1",
                        "Car2",
                        "Car3",
                        "CarPizza",
                        "Flatbed1",
                        "Flatbed2",
                        "Flatbed3",
                        "Hatchback1",
                        "Hatchback2",
                        "Hatchback3",
                        "Pickup1",
                        "Pickup2",
                        "Pickup3",
                        "Semi1",
                        "Semi2",
                        "Semi3",
                        "SportsCar1",
                        "SportsCar2",
                        "SportsCar3",
                        "SUV1",
                        "SUV2",
                        "SUV3",
                        "Taxi",
                        "TowTruck1",
                        "TowTruck2",
                        "TowTruck3",
                        "Ute1",
                        "Ute2",
                        "Ute3",
                        "Van1",
                        "Van2",
                        "Van3",
                        "Motorbike1",
                        "Motorbike2",
                        "Motorbike3"}),
                    new (new string[] {
                        "Cessna1",
                        "Cessna2",
                        "Cessna3",
                        "Cessna4",
                        "SmallHelo1",
                        "SmallHelo2",
                        "SmallHelo3",
                        "Biplane1",
                        "Biplane2"}),
                    new (new string[] {
                        "Boat1",
                        "Boat2",
                        "Boat3"}),
                    new (new string[] {
                        "Metro"}),
                    new (new string[] {
                        "Car1",
                        "Car2",
                        "Car3",
                        "Trailer",
                        "TrailerContainer1",
                        "TrailerContainer2",
                        "TrailerContainer3",
                        "TrailerContainer4",
                        "MetroCarriage"}),
                },
                new Upgrades[0]),
            new MyJobInfo(
                typeof(Prisoner), "Prisoner",
                "The prison door can be bombed and the guard might have a key",
                CharacterType.Humanoid, 0, GroupIndex.Prisoner, new ColorStruct(1f, 0.5f, 0f), 0f, 0,
                new Transports[] {
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (0,
                        new InventoryStruct[] {
                            new ("TopPrisoner", 1),
                            new ("PantsPrisoner", 1)})}),
            new MyJobInfo(
                typeof(Hitman), "Hitman",
                "Assasinate designated targets to earn bounty rewards",
                CharacterType.Humanoid, 0, GroupIndex.Criminal, new ColorStruct(0f, 0f, 0f), 0.01f, 3,
                new Transports[] {
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("TopStriped", 1),
                            new ("SkiMaskDark", 1),
                            new ("GlovesFingerlessDark", 1),
                            new ("Knife", 1)}),
                    new (25,
                        new InventoryStruct[] {
                            new ("SkiMaskLight", 1),
                            new ("Winchester", 1),
                            new ("AmmoRifle", 30)})
                    ,
                    new (30,
                        new InventoryStruct[] {
                            new ("SkiMaskPattern", 1),
                            new ("LaserRed", 1),
                            new ("Silencer", 1)})
                }),
            new MyJobInfo(
                typeof(Police), "Police",
                "Search others for illegal items, arrest criminals, put the them in your car, and bring to jail for cash rewards",
                CharacterType.Humanoid, 0, GroupIndex.LawEnforcement, new ColorStruct(0f, 1f, 1f), 0.03f, 10,
                new Transports[] {
                    new (new string[] { "CarPolice" }),
                    new (new string[0]),
                    new (new string[] { "PoliceBoat" }),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("TopPolice", 1),
                            new ("PantsPolice", 1),
                            new ("CapPolice", 1),
                            new ("Taser", 1),
                            new ("AmmoTaser", 12),
                            new ("Handcuffs", 5),
                            new ("Muzzle", 2),
                            new ("LicenseGun", 1),
                            new ("ShoesBrown", 1)}),
                    new (25,
                        new InventoryStruct[] {
                            new ("Glock", 1),
                            new ("AmmoPistol", 68),
                            new ("Baton", 1),
                            new ("LicenseDrivers", 1)}),
                    new (30,
                        new InventoryStruct[] {
                            new ("TopSheriff", 1),
                            new ("PantsSheriff", 1),
                            new ("CapSheriff", 1),
                            new ("Colt", 1),
                            new ("GlovesDark", 1),
                            new ("KeyPrison", 1)})
                }),
            new MyJobInfo(
                typeof(Paramedic), "Paramedic",
                "Use map to find hurt and knocked out players to heal and revive",
                CharacterType.Humanoid, 0, GroupIndex.Citizen, new ColorStruct(1f, 0.75f, 0.75f), 0.02f, 5,
                new Transports[] {
                    new (new string[] {"Ambulance"}),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("TopParamedic", 1),
                            new ("PantsParamedic", 1),
                            new ("GlovesMedical", 1),
                            new ("HealthPack", 8)}),
                    new (25,
                        new InventoryStruct[] {
                            new ("Splint", 8),
                            new ("Defibrillator", 1),
                            new ("LicenseDrivers", 1)}),
                    new (30,
                        new InventoryStruct[] {
                            new ("TopDoctor", 1),
                            new ("PantsDoctor", 1),
                            new ("Morphine", 6),
                            new ("Bandage", 6)})
                }),
            new MyJobInfo(
                typeof(Firefighter), "Firefighter",
                "Use map to find fires to extinguish",
                CharacterType.Humanoid, 0, GroupIndex.Citizen, new ColorStruct(1f, 1f, 0f), 0.01f, 3,
                new Transports[] {
                    new (new string[] {"FireTruck"}),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("JacketFire", 1),
                            new ("PantsFire", 1),
                            new ("HatHazard", 1),
                            new ("FireExtinguisher", 1)}),
                    new (25,
                        new InventoryStruct[] {
                            new ("HatFire", 1),
                            new ("GlovesMedium", 1),
                            new ("LicenseDrivers", 1)}),
                    new (30,
                        new InventoryStruct[] {
                            new ("JacketFireBlack", 1),
                            new ("PantsFireBlack", 1),
                            new ("FireHose", 1)})
                }),
            new MyJobInfo(
                typeof(Gangster), "Rojo Loco",
                "Kill enemy gangs to start a turf war and defeat enemy waves to capture territory",
                CharacterType.Humanoid, 0, GroupIndex.Criminal, new ColorStruct(1f, 0f, 0f), 0.1f, 8,
                new Transports[] {
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("GangJacketRed", 1),
                            new ("PantsLightBlue", 1),
                            new ("BackwardsCapRed", 1),
                            new ("Machete", 1)}),
                    new (25,
                        new InventoryStruct[] {
                            new ("Sig", 1),
                            new ("AmmoPistol", 48)}),
                    new (30,
                        new InventoryStruct[] {
                            new ("Mac", 1),
                            new ("AmmoSMG", 90)})
                }),
            new MyJobInfo(
                typeof(Gangster), "Green St. Fam",
                "Kill enemy gangs to start a turf war and defeat enemy waves to capture territory",
                CharacterType.Humanoid, 0, GroupIndex.Criminal, new ColorStruct(0f, 1f, 0f), 0.1f, 8,
                new Transports[] {
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("GangJacketGreen", 1),
                            new ("PantsGreen", 1),
                            new ("FaceScarfDark", 1),
                            new ("BatMetal", 1)}),
                    new (25,
                        new InventoryStruct[] {
                            new ("Glock", 1),
                            new ("AmmoPistol", 68)}),
                    new (30,
                        new InventoryStruct[] {
                            new ("MP5SD", 1),
                            new ("AmmoSMG", 60)})
                }),
            new MyJobInfo(
                typeof(Gangster), "Borgata Blu",
                "Kill enemy gangs to start a turf war and defeat enemy waves to capture territory",
                CharacterType.Humanoid, 0, GroupIndex.Criminal, new ColorStruct(0f, 0f, 1f), 0.1f, 8,
                new Transports[] {
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("JacketBusinessBlack", 1),
                            new ("SlacksGray", 1),
                            new ("HatFedora", 1),
                            new ("Crowbar", 1),
                            new ("GlovesFingerlessDark", 1),
                            new ("ShoesBlack", 1)}),
                    new (25,
                        new InventoryStruct[] {
                            new ("PPK", 1),
                            new ("AmmoPistol", 49)}),
                    new (30,
                        new InventoryStruct[] {
                            new ("Shotgun", 1),
                            new ("AmmoShotgun", 32)})
                }),
            new MyJobInfo(
                typeof(Mayor), "Mayor",
                "You're the Mayor: Accept or reject license requests",
                CharacterType.Humanoid, 1, GroupIndex.Citizen, new ColorStruct(1f, 0f, 1f), 0f, 0,
                new Transports[] {
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (0,
                        new InventoryStruct[] {
                            new ("JacketBusinessRed", 1),
                            new ("SlacksGray", 1),
                            new ("ShoesGray", 1),
                            new ("HatBoonieDark", 1),
                            new ("LicenseDrivers", 1),
                            new ("LicenseBoating", 1),
                            new ("LicensePilots", 1),
                            new ("LicenseTrain", 1),
                            new ("LicenseGun", 1)})
                }),
            new MyJobInfo(
                typeof(DeliveryMan), "Delivery Man",
                "Deliver food to hungry players and NPCs on your map (M) for rewards",
                CharacterType.Humanoid, 0, GroupIndex.Citizen, new ColorStruct(0.5f, 0.25f, 0f), 0f, 0,
                new Transports[] {
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("TeePizza", 1),
                            new ("CapPizza", 1)}),
                    new (25,
                        new InventoryStruct[] {
                            new ("LicenseDrivers", 1)}),
                    new (30,
                        new InventoryStruct[] {
                            new ("JacketRacerRed", 1),
                            new ("PantsRacerRed", 1),
                            new ("CapRacerRed", 1),
                            new ("GlovesFingerlessWhite", 1)})
                }),
            new MyJobInfo(
                typeof(TaxiDriver), "Taxi Driver",
                "Bring NPCs to destinations on your map (M) for rewards",
                CharacterType.Humanoid, 0, GroupIndex.Citizen, new ColorStruct(0f, 0f, 0.5f), 0f, 0,
                new Transports[] {
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("CapFlat", 1),
                            new ("SuspendersBrown", 1),
                            new ("LicenseDrivers", 1)}),
                    new (25,
                        new InventoryStruct[] {
                            new ("GlovesWhite", 1),
                            new ("LicensePilots", 1)}),
                    new (30,
                        new InventoryStruct[] {
                            new ("TopChauffeur", 1),
                            new ("PantsChauffeur", 1),
                            new ("CapChauffeur", 1)})
                }),
            new MyJobInfo(
                typeof(SpecOps), "SpecOps",
                "Hunt down the most wanted players on the server for rewards",
                CharacterType.Humanoid, 0, GroupIndex.LawEnforcement, new ColorStruct(0.75f, 0.75f, 0.25f), 0.015f, 8,
                new Transports[] {
                    new (new string[] {
                        "TroopCar1",
                        "TroopCar2",
                        "TroopCar3",
                        "ArmyFuel1",
                        "ArmyFuel2",
                        "ArmyFuel3",
                        "ArmoredCar1",
                        "ArmoredCar2",
                        "ArmoredCar3" }),
                     new (new string[] {
                        "Apache1",
                        "Apache2",
                        "Apache3",
                        "Fighter1",
                        "Fighter2",
                        "FighterBig1",
                        "FighterBig2"}),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("TopCombat", 1),
                            new ("PantsCombat", 1),
                            new ("HelmetCombat", 1),
                            new ("Sig", 1),
                            new ("AmmoPistol", 49),
                            new ("Smoke", 1),
                            new ("LicenseGun", 1)}),
                    new (25,
                        new InventoryStruct[] {
                            new ("Kabar", 1),
                            new ("GlovesFingerlessDark", 1),
                            new ("LicenseDrivers", 1)}),
                    new (30,
                        new InventoryStruct[] {
                            new ("M4", 1),
                            new ("AmmoRifle", 60)})
                }),
            new MyJobInfo(
                typeof(Retriever), "Retriever",
                "Return lost or dropped items to their rightful owner in time for rewards",
                CharacterType.Mob, 0, GroupIndex.Citizen, new ColorStruct(0.25f, 0.75f, 0.25f), 0f, 0,
                new Transports[] {
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                    new (new string[0]),
                },
                new Upgrades[] {
                    new (20,
                        new InventoryStruct[] {
                            new ("CapMob", 1)}),
                    new (25,
                        new InventoryStruct[] { }),
                    new (30,
                        new InventoryStruct[] { })
                }),
            };
    }
}
