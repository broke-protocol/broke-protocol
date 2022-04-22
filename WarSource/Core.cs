using BrokeProtocol.API;
using BrokeProtocol.GameSource.Jobs;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Jobs;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace BrokeProtocol.GameSource
{
    public class Core : Plugin
    {
        public Core()
        {
            Info = new PluginInfo(
                "WarSource",
                "war",
                "War Plugin for BP. May be modified.",
                "https://github.com/broke-protocol/broke-protocol");

            string jobsFilename = Info.Name + " Jobs.json";

            if (!File.Exists(jobsFilename))
            {
                File.WriteAllText(jobsFilename, JsonConvert.SerializeObject(GetJobs, Formatting.Indented));
            }

            // Use JobsAdditive if you're adding to Default jobs and not replacing them
            JobsOverride = JsonConvert.DeserializeObject<List<JobInfo>>(File.ReadAllText(jobsFilename));
        }

        private List<JobInfo> GetJobs => new List<JobInfo> {
            new JobInfo(
                typeof(Gangster), "SpecOps",
                "Kill enemy gangs to start a turf war and defeat enemy waves to capture territory",
                CharacterType.Human, 0, GroupIndex.Gang, 0, new ColorStruct(1f, 0f, 0f), 0.1f, 8,
                new Transports[] {
                    new Transports(new string[0]),
                    new Transports(new string[0]),
                    new Transports(new string[0])
                },
                new Upgrades[] {
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("GangJacketRed", 1),
                            new InventoryStruct("PantsLightBlue", 1),
                            new InventoryStruct("BackwardsCapRed", 1),
                            new InventoryStruct("Machete", 1)}),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("Sig", 1),
                            new InventoryStruct("AmmoPistol", 48)}),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("Mac", 1),
                            new InventoryStruct("AmmoSMG", 90)})
                }),
            new JobInfo(
                typeof(Gangster), "OpFor",
                "Kill enemy gangs to start a turf war and defeat enemy waves to capture territory",
                CharacterType.Human, 0, GroupIndex.Gang, 0, new ColorStruct(0f, 1f, 0f), 0.1f, 8,
                new Transports[] {
                    new Transports(new string[0]),
                    new Transports(new string[0]),
                    new Transports(new string[0])
                },
                new Upgrades[] {
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("GangJacketGreen", 1),
                            new InventoryStruct("PantsGreen", 1),
                            new InventoryStruct("FaceScarfDark", 1),
                            new InventoryStruct("BatMetal", 1)}),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("Glock", 1),
                            new InventoryStruct("AmmoPistol", 68)}),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("MP5SD", 1),
                            new InventoryStruct("AmmoSMG", 60)})
                }),
            };
    }
}
