# CommandHandler

This document will explain how to create a command, register one, and use it along with the GroupManager to only allow it for specific groups.

See `Commands.cs` in [GameSource](https://github.com/broke-protocol/broke-protocol/blob/master/GameSource/Commands.cs) for an example.

Define your custom commands within an IScript class. The first input parameter must be an ShPlayer type (since the command will always send the calling player as a parameter). Optional/Default parameters you define will also be optional in-game. Any string parameters with spaces should start and end with double-quotes when called: `/ban "John Smith" "Some Reason"`.

```csharp
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

        // Any optional parameters here will be optional with in-game commands too
        public void OnCommandInvoke(ShPlayer player, ShPlayer target, string string1 = "default1", string string2 = "default2", byte byte1 = 1, int int1 = 2, float float1 = 3f)
        {
            player.svPlayer.SendGameMessage($"'{target.username}' '{string1}' '{string2}' '{byte1}' '{int1}' '{float1}'");
        }
    }
```

Every command has a group namespace and name (case-insensitive).
So in `GameSource` its defined in `Core.cs` 

```Info = new PluginInfo("GameSource", "game");```

>"game" is the namespace here.

So to add the `example` command from the `game` plugin you add `"game.example"` under your groups.json permissions list.
