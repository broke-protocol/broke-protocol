using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Utility.Jobs;
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
            if (player.job.info.groupIndex != GroupIndex.LawEnforcement)
            {
                player.svPlayer.SendGameMessage($"Warning! You are about to enter {triggerID}!");

                /* Execute client C# example */
                //player.svPlayer.ExecuteCS("clManager.SendToServer(Channel.Unsequenced, SvPacket.GlobalMessage, \"ExecuteCS Test\");");

                /* Execute client C# via JavaScript example */
                /* Note inner quote is escaped twice due to being unwrapped across 2 languages */
                //player.svPlayer.ExecuteJS("exec(\"clManager.SendToServer(Channel.Unsequenced, SvPacket.GlobalMessage, \\\"ExecuteJS Test\\\");\");");
            }
        }
    }

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
