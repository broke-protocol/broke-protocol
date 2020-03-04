using BrokeProtocol.API;
using BrokeProtocol.Entities;
using System;

namespace BrokeProtocol.CustomEvents
{
    public class ExampleCommand : IScript
    {
        public ExampleCommand()
        {
            CommandHandler.RegisterCommand("Example", new Action<ShPlayer, ShPlayer, string, string, byte, int, float>(OnCommandInvoke), (player, command) =>
            {
                // Silly example
                if (player.health < 50)
                {
                    player.svPlayer.SendGameMessage("Must be over 50 health to use this command");
                    return false;
                }
                return true;
            });
        }

        public void OnCommandInvoke(ShPlayer player, ShPlayer target, string string1, string string2, byte byte1, int int1, float float1)
        {
            player.svPlayer.SendGameMessage($"'{target.username}' '{string1}' '{string2}' '{byte1}' '{int1}' '{float1}'");
        }
    }
}
