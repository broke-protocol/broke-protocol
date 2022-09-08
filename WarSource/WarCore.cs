using BrokeProtocol.API;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Jobs;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace BrokeProtocol.GameSource
{
    public class WarCore : Plugin
    {
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
        }

        private List<JobInfo> GetJobs => new List<JobInfo> {
            new JobInfo(
                typeof(Army), "SpecOps",
                "Kill enemy gangs to start a turf war and defeat enemy waves to capture territory",
                CharacterType.Human, 0, new ColorStruct(1f, 0f, 0f),
                new Upgrades[] {
                    new Upgrades(
                        new InventoryStruct[] { } ),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("GrenadeSmoke", 3)}),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("KevlarVest", 1)}),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("Grenade", 3)}),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("HelmetCombat", 1)})
                }),
            new JobInfo(
                typeof(Army), "OpFor",
                "Kill enemy gangs to start a turf war and defeat enemy waves to capture territory",
                CharacterType.Human, 0, new ColorStruct(0f, 1f, 0f),
                new Upgrades[] {
                    new Upgrades(
                        new InventoryStruct[] { } ),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("GrenadeSmoke", 3)}),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("KevlarVest", 1)}),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("Grenade", 3)}),
                    new Upgrades(
                        new InventoryStruct[] {
                            new InventoryStruct("RiotHelmet", 1)})
                }),
            };
    }
}
