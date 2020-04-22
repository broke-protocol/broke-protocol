using BrokeProtocol.API;
using BrokeProtocol.Entities;
using System;

namespace BrokeProtocol.CustomEvents
{
    public class ExampleCommand : IScript
    {
        public ExampleCommand()
        {
            CommandHandler.RegisterCommand("Example", new Action<ShPlayer, ShPlayer, byte, int, float, string, string>(OnCommandInvoke), (player, command) =>
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


        // Any optional parameters here will be optional with in-game commands too
        public void OnCommandInvoke(ShPlayer player, ShPlayer target, byte byte1 = 1, int int1 = 2, float float1 = 3f, string string1 = "default1", string string2 = "default2")
        {
            player.svPlayer.SendGameMessage($"'{target.username}' '{byte1}' '{int1}' '{float1}' '{string1}' '{string2}'");
        }
    }
}
