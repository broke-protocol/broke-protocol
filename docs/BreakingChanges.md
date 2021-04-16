## 1.12
?> The Apartment Raiding Update changes some API related to Jails, Animation functions/packets, and AI states but most changes just add new functionality. New events related to Inventories and DestroySelf (suicide) could be useful. Servers can now request to open URLs on clients and a new class of ShSecurity items are added.

### API
- Multiple jails support: SceneManager.Instance.jail => svManager.jails
    - Use svManager.jails.GetRandom() to pick randomly from all jails
- Added svPlayer.SvOpenURL(string url, string title)
- Added svPlayer.StartHackingMenu(string title, int targetID, string menuID, string optionID, float difficulty)
- Renamed all SvAnimate*() functions to SvAnimator*() for consistency
- Removed trySell and chatted LimitQueues for cooldown timers, define your own like in GameSource
- SvAnimator*() functions moved to SvEntity base class so it can be called on any Entity with an Animator component
- SvAnimatorEnabled(bool enabled) function added
- Added GameSource Events:
    - DestroyableDestroySelf
    - PlayerDestroySelf (Suicide)
    - EntityTransferItem
    - PlayerTransferItem
    - PlayerHackFinished

### MODDING
- ProcessOptions now also take an equipableName for auto-checking/equiping an item during a Processing (leave blank to use 'Hands', backwards compatible)
- Added vehicle/mount stances for modding: StandStill, CrouchStill, StandFixed, CrouchFixed

### Misc
- BUG: Players invited to Apartments will trigger trespassing crimes if detected by Security items. Will fix soon.

## 1.12 Hotfix #3
?> Coroutines (loops that perform continuous non-blocking work) on Entities were piling up on every respawn. The effect was server performance degration after significant uptime. Now all coroutines on Entities are cleared at the start of every Spawn() call. So if you had job coroutines or similar started on an Entities before, use Respawn events or the new Job.OnSpawn() callback to start them fresh each time.

### API
- Added Job.OnSpawn() virtual method
- ShPlayer.RemoveItemsJail() -> SvPlayer.RemoveItemsJail()
- ShPlayer.RemoveItemsDeath() -> SvPlayer.RemoveItemsDeath(bool dropItems)
- Briefcase drop logic moved from PlayerDeath event to PlayerRemoveItemsDeath event
- Job.OnDamageEntity and Job.OnDestroyEntity have their crime handling added to GameSource

## 1.12
?> The 2021 Update introduces a jumble of minor changes and additions. Adds API around user input: Apartment Security Panel code in GameSource serves as an example. New animation Packets/Functions to run custom character animations on clients. New GameSource events and functions around spectate functionality added.

### API
- Removed Channel.Fragmented: Channel.Reliable can now support large packets in the same way
- Added svPlayer.SvSpectate(ShPlayer target)
- Added svPlayer.SendInputMenu(...)
- player.looking -> player.pointing
- Added GameSource Player Events:
    - OnSubmitInput: When a player submits input via the new text input API
    - OnPoint: When a player starts/stops pointing (point location as argument)
    - OnAlert: When a player hits the alert button (whistling/beeping)
    - OnReady: When player is first spawned and ready (Sends ServerInfo window in GameSource)

### MODDING
- Custom Player animation parameters can now be set on clients to trigger custom animations
    - SvPlayer.SvAnimateFloat(string parameterName, float value)
    - SvPlayer.SvAnimateInt(string parameterName, int value)
    - SvPlayer.SvAnimateBool(string parameterName, bool value)
    - SvPlayer.SvAnimateTrigger(string parameterName)
- Support for poisonous consumable mods (set negative health boost)

### Misc
- SvRestore has new optional interior/parent argument

## 1.11
?> The AI Overhaul Update. Adds API methods and Events around player display names. Minor naming changes and useful helper functions added as well.

### API
- Added GameSource Player Event: OnDisplayName -> Custom formatting for displayed names across playerlist, overhead, and chat on join
- Added svPlayer.SvUpdateDisplayName() -> Change and sync display name updates at runtime to all clients - Color codes supported
- Helper functions to iterate and test local entities (Used in GameSource Jobs as an example)
    - svEntity.LocalEntitiesAll(Test, Action) -> Perform Test and Action (if true) on all entities in rendering range
    - svEntity.LocalEntitiesOne(Test, Action) -> Same as above but stops at the first entity that Test == true
- entity.GetVelocity() -> entity.Velocity
- player.fullname -> player.displayName

### MODDING
- Fixed longstanding issues with splines and heightmaps - loaded correctly now
- Larger map/asset sizes are supported now

### Misc
- Color codes for OptionMenu and TextMenu are supported across all options and menu titles too
- Don't use svPlayer.SetState(StateIndex.Attack) anymore
    - Use svPlayer.SetAttackState() and svPlayer.SetFollowState() helper functions

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
