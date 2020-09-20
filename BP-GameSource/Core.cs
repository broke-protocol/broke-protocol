using BrokeProtocol.API;
using BrokeProtocol.Utility.Jobs;
using BrokeProtocol.GameSource.Jobs;

namespace BrokeProtocol.GameSource
{
    public class Core : Plugin
    {
        public Job[] jobs = new Job[] {
            new Citizen(),
            new Criminal(),
            new Prisoner(),
            new Police(),
            new Paramedic(),
            new Firefighter(),
            new Gangster(),
            new Gangster(),
            new Gangster(),
            new Mayor(),
            new DeliveryDriver(),
            new TaxiDriver(),
            new SpecOps()
        };


        public Core()
        {
            Info = new PluginInfo("GameSource", "game")
            {
                Description = "Default game source used by BP. May be modified.",
                Website = "https://github.com/broke-protocol/broke-protocol"
            };
        }
    }
}
