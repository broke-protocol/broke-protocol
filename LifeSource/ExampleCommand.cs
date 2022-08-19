using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using System;

namespace BrokeProtocol.CustomEvents
{
    public class ExampleCommand : IScript
    {
        public ExampleCommand()
        {
            CommandHandler.RegisterCommand("ClearCrimes", new Action<ShPlayer, ShPlayer>(ClearCrimes), null, "example.clearcrimes");
        }

        public void ClearCrimes(ShPlayer player, ShPlayer target = null)
        {
            if (!target) target = player;

            if (LifeManager.pluginPlayers.TryGetValue(target, out var pluginPlayer))
            {
                pluginPlayer.ClearCrimes();
            }
        }
    }
}
