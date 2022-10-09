using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using System;

namespace BrokeProtocol.GameSource
{
    public class LifeCommands : IScript
    {
        public LifeCommands()
        {
            CommandHandler.RegisterCommand("ClearCrimes", new Action<ShPlayer, ShPlayer>(ClearCrimes), null, Utility.adminPermission);
        }

        public void ClearCrimes(ShPlayer player, ShPlayer target = null)
        {
            if (!target) target = player;
            
            target.LifePlayer().ClearCrimes();
        }
    }
}
