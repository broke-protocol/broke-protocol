## 1.1
?> The Hitman Update. Reworks basically all of the API around Jobs to support modding. Custom Jobs can now be defined in Plugins.

### API
- Added BPAPI.Instance.Jobs -> Stores all Job metadata
- Added JobInfoShared class -> Contains Job metadata that is sent to clients
- ShPlayer.job -> Split into SvPlayer.job (Server Job info) and ClPlayer.job (Client Job info)
- Added EntityCollections.RandomPlayer and RandomNPC
- Added SendOptionMenu() UI helper function
- FunctionMenu replaced with TextPanel (use OptionMenu for input handling)
- Old TextPanel functions renamed to TextMenu across API
- Updated SendTextMenu() with optional window size parameters
- More GetEntity() overloads to use entity names directly
- Added ManagerLoad, Unrestrain, JailCriminal, and GoToJail events to GameSource
- Persistent server storage now available in svManager.database.Data collection
- Session server storage now available at svManager.sessionData
- Added CustomData.ConvertData<T> helper static function

### MODDING
- Asset Mods can have eventActions defined for custom Action Menu options
- Added Button entity as an example (new custom ButtonPush event defined in GameSource)
- Added 'entity.svEntity.data' string field for custom storage (set in World Builder or via code)
- Removed 'identifier' field from Triggers, use data field instead

### Misc
- JobName and JobGroup type name changes in groups.json
- Apartments can have custom furniture limited set now (in World Builder)

## 1.08
?> The Apps Update. Biggest changes based around new Apps for messaging, calls, and banking. Old ATM system replaced.

### API
- Added ExecutionMode.Test and ExecutionMode.PostEvent to EventHandler [Explained here](https://broke-protocol.github.io/broke-protocol/#/Examples/Server/Events?id=subscribing-to-a-game-event)
- Added EntityCollections.Accounts (HashSet<string>) for fast login lookups
- All Cl/SvPacket types changed to enums
- Replace ServerInfo with generic TextPanel and helper function svPlayer.SendTextPanel(string title, string text)

### MODDING
- ATM class of items removed
- ATMs, phones, and other electronic device mods must have AvailableApps property set

### Misc
- Added /clearwanted and /deleteaccount commands (updated groups.json)

## 1.07a
?> Update to Unity 2020.1f1. Big engine update but minimal API changes. [Full 2020.1 Release Notes](https://unity3d.com/unity/whats-new/2020.1.0)

### API
- ShPlayer.currentTrigger => ShPlayer.currentTriggers (HashSet<ShTrigger> of all triggers in case of trigger nesting)
- ClManager.handler and SvManager.handler packet handlers converted from Dictionary to Lists
- BrokeProtocol.Server.LiteDB + Models namespaces => BrokeProtocol.LiteDB (used on client now for some things too)

### MODDING
- Processors must be updated to the new multi-process support (see ShProcessor.processOptions)
- Added ShProcessor.processDuration for process duration modding

### Misc
- groups.json: JobIndex and JobGroupIndex Types now use the string job/class names instead of ints (documented in groups.json)
- groups.json: ScriptableTrigger Type removed for now (due to more complex trigger handling)

## 1.07
?> Biggest breaker might be the update to LiteDB V5. Game will attempt to backup and upgrade your V4 database on server start. If you get errors with CustomData, you may try to repair things with these GUI editors and try to upgrade again: [V4 DB Editor](https://github.com/julianpaulozzi/LiteDbExplorer) or [V5 DB Editor](https://github.com/mbdavid/LiteDB.Studio)

### API
- Steam Authentication removed: AuthData and ConnectData merged into ConnectionData class
- ShPlayer.accountID removed: Use ShPlayer.username for the database index now
- SvPlayer.SvResetJob() always adds required job items now
- Added SvContact.spawnFires if you want a thrown item to leave a fire on contact

### MODDING
- Added `skins.txt` for modding available skins during registration (custom skins allowed)
- Game supports apartments within interiors again (as long as apartments aren't nested in apartments)

### Misc
- All ban functionality is now stored and tied to IP addresses, not accounts

## 1.06
?> Most additions are related to the new attachments and asset modding

### API
- Only 1 active jail per map now: ShManager.jails -> SceneManager.Instance.jail
- Added ShPlayer.SetPositionSafe()
- Added SvPlayer.SvBindAttachment()
- Added SvPlayer.SvUnbindAttachment()
- Added SvPlayer.SvSetAttachment()
- Added GameSource Event: OnServerInfo(ShPlayer player)

### MODDING
- Added ShEntoty.collectedItems[] for a custom set of collected items when picked up
- Added ShGun.fireEffect for preset particle effects to save filesize and memory (remove your custom effects)
- Added Attachment, Muzzle, Sight, Underbarrel classes of entities

## 1.05
?> Most additions are related to the new injury system

- Added GameSource Event: OnRestrain(ShPlayer player, ShRestrained restrained)
- Added BPAPI.Instance.Plugins
- Changed GameSource Event: OnDamage -> new `float hitY` parameter for locational damage
- Recommend to use Channel.Reliable for all Game Messages 
- Removed BrokeProtocol.Collections.LimitQueue.Add(T item);
- Changed BrokeProtocol.Collections.LimitQueue.OverLimit(T item) -> Limit(T item, bool add = true)
