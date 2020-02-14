<!-- panels:start -->
!> This page is not finalized. PR's are welcome.

## Intro
Values `0` and `1` will always be the following in an event enum:
```
Invalid,
None
```
These two will be ignored.

# General

<!-- div:title-panel -->
## OnAPIReady `Action`

<!-- div:left-panel -->
> Gets called when the API is ready and done registering **all** plugins.

**Path:** `BrokeProtocol.API.Events.General.OnAPIReady`  
**Delegate:** `Action`
<!-- div:right-panel -->
```csharp
public class OnApiReady : IScript
{
    public OnApiReady()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.General.OnAPIReady, new Action(OnEvent));
    }
    public void OnEvent()
    {
        // At this point, all plugins should be loaded in. You can now use things like
        Plugins.HasPluginLoaded("YourPluginName") // true
        Plugins.GetPlugin("YourPluginName") // Plugin
        Plugins.GetPluginCount() // 2
        // And more!
    }
}
```

<!-- div:title-panel -->
## OnAPIReadySingle `Action<Plugin>`

<!-- div:left-panel -->
> Gets called when the API is ready registering **one, single** plugin. The `Plugin` instance will be passed as first argument.

**Path:** `BrokeProtocol.API.Events.General.OnAPIReadySingle`  
**Delegate:** `Action<Plugin>`
<!-- div:right-panel -->
```csharp
public class OnApiReadySingle : IScript
{
    public OnApiReadySingle()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.General.OnAPIReadySingle, new Action<Plugin>(OnEvent));
    }
    public void OnEvent(Plugin plugin)
    {
        var name = plugin.GetPluginName();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        if (name != "otherplugin")
        {
            return;
        }
        // A plugin was loaded by the name of `otherplugin`, but beware: this can still be a impersonation of the plugin you're trying to find.
        Debug.Log("Our cool plugin was loaded in! Now, do epic stuff!");
        // For example, get the CustomData value by key "isUserR00TCool"
        if (!plugin.CustomData.TryFetchCustomData<string>("isUserR00TCool", out var isCool))
        {
            // Key was not found, exit codeblock
            return;
        }
        Debug.Log($"Is UserR00T cool? {(isCool ? "Yes" : "No")}"); // hopefully this returns yes :)
    }
}
```

<!-- div:title-panel -->
## OnConsoleInput `Action<string>`

<!-- div:left-panel -->
> This event gets called the moment something gets entered into the console. This is very low level, and does not provide a `ConsoleCommand` class. (this might be added in post 1.0.). Also **note**, there are a few pre registered commands (like `save` and `exit`) but this event gets called **before** they get called, thus, there is no way to override the command behavior.

**Path:** `BrokeProtocol.API.Events.General.OnConsoleInput`  
**Delegate:** `Action<string>`

<!-- div:right-panel -->
```csharp
public class OnConsoleInput : IScript
{
    public OnConsoleInput()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.General.OnConsoleInput, new Action<string>(OnEvent));
    }
    public void OnEvent(string str)
    {
        // Check if the string is the actual ban command, else exit.
        if (!str.StartsWith("ban", StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }
        // Split the string on spaces so it's easier to check for arguments. We expect the following array:
        // data[0]: command name/input string
        // data[1]: steamid
        // data[2+]: anything after index 1 can be considered the reason
        var data = str.Split(' ');
        // Make sure we have at least the amount specified above
        if (data.Length <= 3)
        {
            Debug.LogError("Usage: ban [steamid] [reason] - expected more arguments");
            return;
        }
        // Make sure the SteamID is an actual valid ulong
        if (!ulong.TryParse(data[1], out var steamid))
        {
            Debug.LogError("Invalid steamid format");
            return;
        }
        // Merge reason into one long string, but skip first two arguments because that's the command name and steamid.
        var reason = string.Join(" ", data.Skip(2));

        // This will only work on a online player, but fun little challenge: try to make it work on offline players as well. Check the server Database example for more help with that, as you're gonna need to search through the user collection by steamid.
        // For this you might need a using statement: 'using System.Linq;`
        // We validated above that it was a ulong, but why are we not using it here? Very easy reason: bugs. We're saving the ID as string due to some truncation errors, hence we need to ToString it everywhere. Do note, the values saved in the database should always be parseable without Try function.
        var target = EntityCollections.Players.FirstOrDefault(x => x.steamID == data[1]);
        if (target == null)
        {
            Debug.LogError("Player does not exist with that ID or is offline.");
            return;
        }

        // Set ban variables.
        target.svPlayer.PlayerData.BanInfo.IsBanned = true;
        target.svPlayer.PlayerData.BanInfo.Date = DateTime.Now;
        target.svPlayer.PlayerData.BanInfo.Reason = reason;

        // Inform console about the happy little... accident.
        Debug.Log($"Banned {target.username} for {reason}.");

        // Disconnect the player so that the database gets saved and so they can't play anymore.
        target.svPlayer.svManager.Disconnect(target.svPlayer.connection, DisconnectTypes.Banned);
    }
}
```

<!-- div:left-panel -->

# Manager

<!-- div:title-panel -->
## OnStarted `Action<SvManager>`

<!-- div:left-panel -->
> Executed once the server is ready.  

**Path:** `BrokeProtocol.API.Events.Manager.OnStarted`  
**Delegate:** `Action<SvManager>`
<!-- div:right-panel -->
```csharp
public class OnStarted : IScript
{
    public OnStarted()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Manager.OnStarted, new Action<SvManager>(OnEvent));
    }
    public void OnEvent(SvManager svManager)
    {
        // Set property to value so it can be used later.
        Core.Instance.SvManager = svManager;
    }
}
```


<!-- div:title-panel -->
## PreSave `Action`

<!-- div:left-panel -->
> This events get executed **before** the server gets saved, but after a save has been requested. (method `SaveAll` invoked) This should be used to save some values before a write commences.

**Path:** `BrokeProtocol.API.Events.Manager.PreSave`  
**Delegate:** `Action`
<!-- div:right-panel -->
```csharp
public class PreSave : IScript
{
    public PreSave()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Manager.PreSave, new Action(OnEvent));
    }
    public void OnEvent()
    {
    }
}
```

<!-- div:title-panel -->
## Save `Action`

<!-- div:left-panel -->
> This events get executed **after** the server has been saved.  
**Note:** When the server gets saved it will automatically save any `CustomData`.

**Path:** `BrokeProtocol.API.Events.Manager.Save`  
**Delegate:** `Action`
<!-- div:right-panel -->
```csharp
public class Save : IScript
{
    public Save()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Manager.Save, new Action(OnEvent));
    }
    public void OnEvent()
    {
    }
}
```

<!-- div:title-panel -->
## OnTryLogin `Action<ShPlayer>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Manager.OnTryLogin`  
**Delegate:** `Action<SvManager, AuthData, ConnectData>`
<!-- div:right-panel -->
```csharp
public class OnTryLogin : IScript
{
    public OnTryLogin()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Manager.OnTryLogin, new Action<SvManager, AuthData, ConnectData>(OnEvent));
    }
    public void OnEvent(SvManager svManager, AuthData authData, ConnectData connectData)
    {
    }
}
```
<!-- div:title-panel -->
## OnTryRegister `Action<ShPlayer>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Manager.OnTryRegister`  
**Delegate:** `Action<SvManager, AuthData, ConnectData>`
<!-- div:right-panel -->
```csharp
public class OnTryRegister : IScript
{
    public OnTryRegister()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Manager.OnTryRegister, new Action<SvManager, AuthData, ConnectData>(OnEvent));
    }
    public void OnEvent(SvManager svManager, AuthData authData, ConnectData connectData)
    {
    }
}
```

<!-- div:left-panel -->

# Entity

<!-- div:title-panel -->
## OnAddItem `Action<ShEntity, int, int, bool>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Entity.OnAddItem`  
**Delegate:** `Action<ShEntity, int, int, bool>`
<!-- div:right-panel -->
```csharp
public class OnAddItem : IScript
{
    public OnAddItem()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Entity.OnAddItem, new Action<ShEntity, int, int, bool>(OnEvent));
    }
    public void OnEvent(ShEntity player, int itemIndex, int amount, bool dispatch)
    {
    }
}
```
<!-- div:title-panel -->
## OnRemoveItem `Action<ShEntity, int, int, bool>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Entity.OnRemoveItem`  
**Delegate:** `Action<ShEntity, int, int, bool>`
<!-- div:right-panel -->
```csharp
public class OnRemoveItem : IScript
{
    public OnRemoveItem()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Entity.OnRemoveItem, new Action<ShEntity, int, int, bool>(OnEvent));
    }
    public void OnEvent(ShEntity player, int itemIndex, int amount, bool dispatch)
    {
    }
}
```

<!-- div:left-panel -->

# Destroyable

<!-- div:title-panel -->
## OnDamage `Action<ShDestroyable, DamageIndex, float, ShPlayer, Collider>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Destroyable.OnDamage`  
**Delegate:** `Action<ShDestroyable, DamageIndex, float, ShPlayer, Collider>`
<!-- div:right-panel -->
```csharp
public class OnDamage : IScript
{
    public OnDamage()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Destroyable.OnDamage, new Action<ShDestroyable, DamageIndex, float, ShPlayer, Collider>(OnEvent));
    }
    public void OnEvent(ShDestroyable destroyable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider)
    {
    }
}
```

<!-- div:title-panel -->
## OnDeath `Action<ShDestroyable>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Destroyable.OnDeath`  
**Delegate:** `Action<ShDestroyable>`
<!-- div:right-panel -->
```csharp
public class OnDeath : IScript
{
    public OnDeath()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Destroyable.OnDeath, new Action<ShDestroyable>(OnEvent));
    }
    public void OnEvent(ShDestroyable destroyable)
    {
    }
}
```
<!-- div:left-panel -->

# Movable

<!-- div:title-panel -->
## OnDamage `Action<ShMovable, DamageIndex, float, ShPlayer, Collider>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Movable.OnDamage`  
**Delegate:** `Action<ShMovable, DamageIndex, float, ShPlayer, Collider>`
<!-- div:right-panel -->
```csharp
public class OnDamage : IScript
{
    public OnDamage()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Movable.OnDamage, new Action<ShMovable, DamageIndex, float, ShPlayer, Collider>(OnEvent));
    }
    public void OnEvent(ShMovable movable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider)
    {
    }
}
```

<!-- div:title-panel -->
## OnDeath `Action<ShMovable>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Movable.OnDeath`  
**Delegate:** `Action<ShMovable>`
<!-- div:right-panel -->
```csharp
public class OnDeath : IScript
{
    public OnDeath()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Movable.OnDeath, new Action<ShMovable>(OnEvent));
    }
    public void OnEvent(ShMovable movable)
    {
    }
}
```

<!-- div:title-panel -->
## OnRespawn `Action<ShMovable>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Movable.OnRespawn`  
**Delegate:** `Action<ShMovable>`
<!-- div:right-panel -->
```csharp
public class OnRespawn : IScript
{
    public OnRespawn()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Movable.OnRespawn, new Action<ShMovable>(OnEvent));
    }
    public void OnEvent(ShMovable movable)
    {
    }
}
```

<!-- div:left-panel -->

# Player

<!-- div:title-panel -->
## OnCommand `Action<ShPlayer, string>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnCommand`  
**Delegate:** `Action<ShPlayer, string>`
<!-- div:right-panel -->
```csharp
public class OnCommand : IScript
{
    public OnCommand()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnCommand, new Action<ShPlayer, string>(OnEvent));
    }
    public void OnEvent(ShPlayer player, string message)
    {
    }
}
```

<!-- div:title-panel -->
## OnGlobalChatMessage `Action<ShPlayer, string>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnGlobalChatMessage`  
**Delegate:** `Action<ShPlayer, string>`
<!-- div:right-panel -->
```csharp
public class OnGlobalChatMessage : IScript
{
    public OnGlobalChatMessage()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnGlobalChatMessage, new Action<ShPlayer, string>(OnEvent));
    }
    public void OnEvent(ShPlayer player, string message)
    {
    }
}
```

<!-- div:title-panel -->
## OnLocalChatMessage `Action<ShPlayer, string>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnLocalChatMessage`  
**Delegate:** `Action<ShPlayer, string>`
<!-- div:right-panel -->
```csharp
public class OnLocalChatMessage : IScript
{
    public OnLocalChatMessage()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnLocalChatMessage, new Action<ShPlayer, string>(OnEvent));
    }
    public void OnEvent(ShPlayer player, string message)
    {
    }
}
```

<!-- div:title-panel -->
## OnInitialize `Action<ShPlayer>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnInitialize`  
**Delegate:** `Action<ShPlayer>`
<!-- div:right-panel -->
```csharp
public class OnInitialize : IScript
{
    public OnInitialize()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnInitialize, new Action<ShPlayer>(OnEvent));
    }
    public void OnEvent(ShPlayer player)
    {
    }
}
```

<!-- div:title-panel -->
## OnDestroy `Action<ShPlayer>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnDestroy`  
**Delegate:** `Action<ShPlayer>`
<!-- div:right-panel -->
```csharp
public class OnDestroy : IScript
{
    public OnDestroy()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.EVENT, new Action<ShPlayer>(OnEvent));
    }
    public void OnEvent(ShPlayer player)
    {
    }
}
```

<!-- div:title-panel -->

## OnDamage `Action<ShPlayer, DamageIndex, float, ShPlayer, Collider>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnDamage`  
**Delegate:** `Action<ShPlayer, DamageIndex, float, ShPlayer, Collider>`
<!-- div:right-panel -->
```csharp
public class OnDamage : IScript
{
    public OnDamage()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnDamage, new Action<ShPlayer, DamageIndex, float, ShPlayer, Collider>(OnEvent));
    }
    public void OnEvent(ShPlayer player, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider)
    {
    }
}
```
<!-- div:title-panel -->
## OnDeath `Action<ShPlayer>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnDeath`  
**Delegate:** `Action<ShPlayer>`
<!-- div:right-panel -->
```csharp
public class OnDeath : IScript
{
    public OnDeath()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnDeath, new Action<ShPlayer>(OnEvent));
    }
    public void OnEvent(ShPlayer player)
    {
    }
}
```
<!-- div:title-panel -->
## OnFunctionKey `Action<ShPlayer, byte>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnFunctionKey`  
**Delegate:** `Action<ShPlayer, byte>`
<!-- div:right-panel -->
```csharp
public class OnFunctionKey : IScript
{
    public OnFunctionKey()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnFunctionKey, new Action<ShPlayer, byte>(OnEvent));
    }
    public void OnEvent(ShPlayer player, byte index)
    {
    }
}
```
<!-- div:title-panel -->
## OnSellApartment `Action<ShPlayer, ShApartment>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnSellApartment`  
**Delegate:** `Action<>`
<!-- div:right-panel -->
```csharp
public class OnSellApartment : IScript
{
    public OnSellApartment()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnSellApartment, new Action<ShPlayer, ShApartment>(OnEvent));
    }
    public void OnEvent(ShPlayer player, ShApartment apartment)
    {
    }
}
```
<!-- div:title-panel -->
## OnRespawn `Action<ShPlayer>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnRespawn`  
**Delegate:** `Action<ShPlayer>`
<!-- div:right-panel -->
```csharp
public class OnRespawn : IScript
{
    public OnRespawn()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnRespawn, new Action<ShPlayer>(OnEvent));
    }
    public void OnEvent(ShPlayer player)
    {
    }
}
```

<!-- div:title-panel -->
## OnReward `Action<ShPlayer, int, int>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnReward`  
**Delegate:** `Action<ShPlayer, int, int>`
<!-- div:right-panel -->
```csharp
public class OnReward : IScript
{
    public OnReward()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.EVENT, new Action<ShPlayer, int, int>(OnEvent));
    }
    public void OnEvent(ShPlayer player, int experienceDelta, int moneyDelta)
    {
    }
}
```
<!-- div:title-panel -->
## OnAcceptRequest `Action<ShPlayer, ShPlayer>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnAcceptRequest`  
**Delegate:** `Action<ShPlayer, ShPlayer>`
<!-- div:right-panel -->
```csharp
public class OnAcceptRequest : IScript
{
    public OnAcceptRequest()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnAcceptRequest, new Action<ShPlayer, ShPlayer>(OnEvent));
    }
    public void OnEvent(ShPlayer player, ShPlayer requester)
    {
    }
}
```

<!-- div:title-panel -->
## OnDenyRequest `Action<ShPlayer, ShPlayer>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnDenyRequest`  
**Delegate:** `Action<ShPlayer, ShPlayer>`
<!-- div:right-panel -->
```csharp
public class OnDenyRequest : IScript
{
    public OnDenyRequest()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnDenyRequest, new Action<ShPlayer, ShPlayer>(OnEvent));
    }
    public void OnEvent(ShPlayer player, ShPlayer requester)
    {
    }
}
```

<!-- div:title-panel -->
## OnCrime `Action<ShPlayer, byte, ShEntity>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnCrime`  
**Delegate:** `Action<ShPlayer, byte, ShEntity>`
<!-- div:right-panel -->
```csharp
public class OnCrime : IScript
{
    public OnCrime()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnCrime, new Action<ShPlayer, byte, ShEntity>(OnEvent));
    }
    public void OnEvent(ShPlayer player, byte crimeIndex, ShEntity victim)
    {
    }
}
```

<!-- div:title-panel -->
## OnKick `Action<ShPlayer, SvManager, ShPlayer, string>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnKick`  
**Delegate:** `Action<ShPlayer, SvManager, ShPlayer, string>`
<!-- div:right-panel -->
```csharp
public class OnKick : IScript
{
    public OnKick()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnKick, new Action<ShPlayer, SvManager, ShPlayer, string>(OnEvent));
    }
    public void OnEvent(ShPlayer player, SvManager svManager, ShPlayer player, string reason)
    {
    }
}
```

<!-- div:title-panel -->
## OnBan `Action<ShPlayer, SvManager, ShPlayer, string>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnBan`  
**Delegate:** `Action<ShPlayer, SvManager, ShPlayer, string>`
<!-- div:right-panel -->
```csharp
public class OnBan : IScript
{
    public OnBan()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnBan, new Action<ShPlayer, SvManager, ShPlayer, string>(OnEvent));
    }
    public void OnEvent(ShPlayer player, SvManager svManager, ShPlayer player, string reason)
    {
    }
}
```

<!-- div:title-panel -->
## OnAddItem `Action<ShPlayer, int, int, bool>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnAddItem`  
**Delegate:** `Action<ShPlayer, int, int, bool>`
<!-- div:right-panel -->
```csharp
public class OnAddItem : IScript
{
    public OnAddItem()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnAddItem, new Action<ShPlayer, int, int, bool>(OnEvent));
    }
    public void OnEvent(ShPlayer player, int itemIndex, int amount, bool dispatch)
    {
    }
}
```
<!-- div:title-panel -->
## OnRemoveItem `Action<ShPlayer, int, int, bool>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnRemoveItem`  
**Delegate:** `Action<ShPlayer, int, int, bool>`
<!-- div:right-panel -->
```csharp
public class OnRemoveItem : IScript
{
    public OnRemoveItem()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnRemoveItem, new Action<ShPlayer, int, int, bool>(OnEvent));
    }
    public void OnEvent(ShPlayer player, int itemIndex, int amount, bool dispatch)
    {
    }
}
```
<!-- div:title-panel -->
## OnRemoveItemsDeath `Action<ShPlayer>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.RemoveItemsDeath`  
**Delegate:** `Action<ShPlayer>`
<!-- div:right-panel -->
```csharp
public class RemoveItemsDeath : IScript
{
    public RemoveItemsDeath()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.RemoveItemsDeath, new Action<ShPlayer>(OnEvent));
    }
    public void OnEvent(ShPlayer player, int itemIndex, int amount, bool dispatch)
    {
    }
}
```
<!-- div:title-panel -->
## OnRemoveItemsJail `Action<ShPlayer>`

<!-- div:left-panel -->
> 

**Path:** `BrokeProtocol.API.Events.Player.OnRemoveItemsJail`  
**Delegate:** `Action<ShPlayer>`
<!-- div:right-panel -->
```csharp
public class OnRemoveItemsJail : IScript
{
    public OnRemoveItemsJail()
    {
        GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnRemoveItemsJail, new Action<ShPlayer>(OnEvent));
    }
    public void OnEvent(ShPlayer player)
    {
    }
}
```

<!-- panels:end -->