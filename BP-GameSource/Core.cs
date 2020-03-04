using BrokeProtocol.API;

namespace BrokeProtocol.GameSource
{
    public class Core : Plugin
    {
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
