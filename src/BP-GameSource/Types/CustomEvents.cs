using BrokeProtocol.API;
using BrokeProtocol.Entities;
using System;

namespace BrokeProtocol.CustomEvents
{
    public class CustomEvents : IScript
    {
        public CustomEvents()
        {
            EventsHandler.Add("AreaWarning", new Action<ShPlayer, string>(AreaWarning));
        }

        public void AreaWarning(ShPlayer player, string triggerID)
        {
            player.svPlayer.SendGameMessage($"Warning! You are about to enter {triggerID}!");
        }
    }
}
