using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using System;

namespace BrokeProtocol.GameSource
{
    public class Commands : IScript
    {
        public const string coinFlip = "coinflip";
        public const string heads = "heads";
        public const string tails = "tails";
        public const string cancel = "cancel";

        public Commands()
        {
            CommandHandler.RegisterCommand("ExampleArgs", new Action<ShPlayer, ShPlayer, byte, int, float, string, string>(PrintArgs), null, Utility.allPermission);
            CommandHandler.RegisterCommand("ExampleTest", new Action<ShPlayer>(HelloWorld), (p, c) => p.health < 50f, Utility.allPermission);
            CommandHandler.RegisterCommand("ExampleDiscord", new Action<ShPlayer>((p) => p.svPlayer.SvOpenURL("https://discord.gg/wEB2ZGU", "Open Official BP Discord")), null, Utility.allPermission);
            CommandHandler.RegisterCommand("CoinFlip", new Action<ShPlayer>(CoinFlip), null, Utility.allPermission);
            CommandHandler.RegisterCommand("SlowMo", new Func<ShPlayer, float, bool>(Events.StartSlowMotion), null, Utility.adminPermission);
        }

        public void HelloWorld(ShPlayer player) => player.svPlayer.SendGameMessage($"Hello world");

        // Any optional parameters here will be optional with in-game commands too
        public void PrintArgs(ShPlayer player, ShPlayer target, byte byte1 = 1, int int1 = 2, float float1 = 3f, string string1 = "default1", string string2 = "default2") =>
            player.svPlayer.SendGameMessage($"'{target.username}' '{byte1}' '{int1}' '{float1}' '{string1}' '{string2}'");

        public void CoinFlip(ShPlayer player)
        {
            player.svPlayer.SendTextPanel(
                "Pick Heads or Tails", 
                coinFlip.ToString() + new Random().Next(), // Send a random trailing number to differentiate menus
                new LabelID[] { new LabelID("Heads", heads), new LabelID("Tails", tails), new LabelID("Cancel", cancel)});
        }
    }
}
