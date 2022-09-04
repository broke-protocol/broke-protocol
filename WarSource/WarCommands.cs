using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.WarSource.Types;
using BrokeProtocol.Managers;
using System;

namespace BrokeProtocol.CustomEvents
{
    public class WarCommands : IScript
    {
        public WarCommands()
        {
            CommandHandler.RegisterCommand("Team", new Action<ShPlayer>(TeamSelect));
            CommandHandler.RegisterCommand("Class", new Action<ShPlayer>(ClassSelect));
        }

        public void TeamSelect(ShPlayer player)
        {
            WarManager.SendTeamSelectMenu(player.svPlayer.connectData.connection);
        }

        public void ClassSelect(ShPlayer player)
        {
            if(SvManager.Instance.connections.TryGetValue(player.svPlayer.connectData.connection, out var connectData) &&
                connectData.customData.TryFetchCustomData(WarManager.teamIndexKey, out int teamIndex))
            {
                WarManager.SendClassSelectMenu(player.svPlayer.connectData.connection, teamIndex);
            }
        }
    }
}
