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
}
