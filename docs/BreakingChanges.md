# Changelog
`C` = Changed
`A` = Added
`R` = Removed

## 1.05
!> Breaking change!
?> Most additions are related to the new injury system
- (A) GameSource Event: OnRestrain(ShPlayer player, ShRestrained restrained)
- (C) GameSource Event: OnDamage -> new `float hitY` parameter for locational damage
- (C) Recommend to use Channel.Reliable for all Game Messages 
- (A) BPAPI.Instance.Plugins
- (R) BrokeProtocol.Collections.LimitQueue.Add(T item);
- (C) BrokeProtocol.Collections.LimitQueue.OverLimit(T item) -> Limit(T item, bool add = true)

## 1.04
!> Breaking change!
?> Important Update: This version allows AssetBundle mods to work on any platform. Also, CEF can be enabled on both Steam and Classic auth.
- (C) https://broke-protocol.github.io/broke-protocol/#/Examples/Server/02_Events?id=subscribing-to-a-game-event
- (A) SvPlayer.platform
- (C) EventHandler.Call -> EventHandler.Exec(), EventHandler.Get(), EventHandler.Get<>()
- (R) BrokeProtocol.API.Events.General
- (R) BrokeProtocol.API.Events.Manager
- (R) BrokeProtocol.API.Events.Entity
- (R) BrokeProtocol.API.Events.Movable
- (R) BrokeProtocol.API.Events.Destroyable
- (R) BrokeProtocol.API.Events.Player
- (R) BrokeProtocol.API.Events.General
- (A) BrokeProtocol.API.GameSourceEvents

## 1.03
!> Breaking change!
?> This version changes many getter functions to Properties. This allows helpful inspection in debug tools and signals the property has no side effects.

### Misc
- (C) ActiveMount() -> GetMount
- (C) ActiveControlled() -> GetControlled
- (C) GetStanceIndex() -> GetStanceIndex
- (C) IsSeatedFirst() -> IsSeatedFirst
- (C) IsDriving() -> IsDriving
- (C) IsPassenger() -> IsPassenger
- (C) IsUp() -> IsUp
- (C) IsMobile() -> IsMobile
- (C) IsRestrained() -> IsRestrained
- (C) IsUnrestricted() -> IsUnrestricted
- (C) IsRestricted() -> IsRestricted
- (C) IsSurrendered() -> IsSurrendered
- (C) IsOutside() -> IsOutside
- (C) GetOrigin() -> GetOrigin
- (C) GetPlace() -> GetPlace
- (C) GetPlaceIndex() -> GetPlaceIndex
- (C) GetPosition() -> GetPosition
- (C) GetPositionT() -> GetPositionT
- (C) GetRotation() -> GetRotation
- (C) GetRotationT() -> GetRotationT

## 1.02a
?> This version shouldn't really break any existing plugins but adds default parameters and extra wrapper functions and settings for additional options.

### Misc
- (A) announcements.txt

### `(static)` groups.json
- (A) `Group.Tag` -> Use for custom Chat/Playerlist Tags
- (A) `bp.environment` -> All previous environment permissions bundled here now
- (A) `bp.transfer` Permission
- (A) `bp.heal` Permission

### `(static)` settings.json
- (A) `announcements`
  - 'announcements.enabled'
  - 'announcements.interval'

### `(src)` BrokeProtocol.API.CommandHandler
- (C) Can now accept optional permission string parameter (uses command name if null)
- (C) Can now accept multiple commands if arguments are different
- (A) Wrapper functions accepting List<string> if command aliases needed

## 1.02
!> Breaking change!

?> This version changed a lot of API stuff with regards to accounts, extension methods, and command handling due to a shift to allow multiple modes of authentication and vanilla Command handling support. Extension Methods should all be in the Util class especially if there's nothing API specific about them. Prefer full caps 'ID' naming for consistency. Prefer fields over Properties due to Unity constraints and consistency.

### Misc
- (R) DiscordSDK (preparation for mobile)
- (R) Client Console handling (preparation for mobile)
- (C) Unity Updated to 2019.2.17f1

### `(static)` groups.json
- (C) `UserSteamID` -> `AccountID` Can refer to both SteamID or AccountID for classic login.
- (A) `bp.defaultEnvironment` Permission
- (A) `bp.customEnvironment` Permission
- (A) `bp.dayFraction` Permission
- (A) `bp.weatherFraction` Permission
- (A) `bp.skyColor` Permission
- (A) `bp.cloudColor` Permission
- (A) `bp.waterColor` Permission
- (A) `bp.banAccount` Permission
- (A) `bp.unbanAccount` Permission
- (A) `bp.summon` Permission
- (A) `bp.help` Permission

### `(static)` settings.json
- (C) `steam` -> `auth`
- (A) `auth.steam`
- (A) `startMoney`

### `(src)` BrokeProtocol.API.ExtensionMethods
- (R) `BrokeProtocol.API.ExtensionMethods`
- (C) `SanitizeString()` -> `Util.CleanMessage()` & `Util.CleanCredential()` Split due to different requirements
- (C) `ParseColorCodes()` -> `Util.ParseColorCodes()`

### `(src)` BrokeProtocol.Entities.SvManager
- (C) `Database` -> `database`

### `(src)` BrokeProtocol.Entities.SvPlayer
- (C) `steamID` -> `accountID`
- (C) `SendChatMessage` -> `SendGameMessage`

### `(src)` BrokeProtocol.Utility.Util
Use reference all special characters in Util. Don't allow modding since username and message sanitization is dependent on that.
- (A) `commandPrefix = '/'`
- (A) `stringDelimiter = '"'`
- (A) `tagDelimiter = '<'`
- (A) `tagDelimiterReplacement = '^'`
- (A) `space = ' '`
- (A) `colorDelimiter = '&'`
  - One stop for all important consts.
- (A) `slowInput`
- (A) `fastInput`
- (A) `maxWantedLevel`
- (A) `startExperience`
- (A) `maxExperience`
- (A) `switchTime`
- (A) `minCredential`
- (A) `maxCredential`

### `(src)` BrokeProtocol.Collections.EntityCollections
- (C) `TryGetPlayerByNameOrId()` -> `TryGetPlayerByNameOrID()`

### `(src)` BrokeProtocol.API.CommandHandler
- (C) Mostly rewritten to allow O(1) lookups, allow spaces for any argument, and fix coalescence issues. Drop support for command aliases due to unnecessary complications.

### `(src)` BrokeProtocol.API.PluginInfo
- (R) TargetVersion
- (R) Git
- (R) Authors

### Other
- (C) Other minor changes include moving server only functions to respective 'Sv' classes and cleaning up duplicated code. 

## 1.01
!> Breaking change!

?> This version added multiple new features and fixes, one of them being a breaking group.json change. This will now allow to select the Member list using different variables, like steamid, ip, jobname/index, etc. 

### Misc
- (R) Removed some C#7 language constructs not Engine compatible
- (C) Unity Updated to 2019.2.16f1

### `(static)` groups.json
- (C) `Users` -> `Members`
- (A) `Type`

### `(src)` BrokeProtocol.API.Group
- (C) `Users` -> `Members`
- (C) `HasOverrideUsers()` -> `HasOverrideMembers()`
- (A) `Type`
- (A) `GroupType`
```csharp
enum GroupType
    {
        UserSteamID,
        UserIP,
        JobIndex,
        JobName,
        JobGroupIndex,
        ScriptableTrigger
    }
```
- (A) `TypeActions`

### `(src)` BrokeProtocol.Entities.SvPlayer
- (A) `currentTrigger`

### `(src)` BrokeProtocol.Entities.SvEntity
- (C) `SvReset()` -> `SvRestore()`
- (C) `Reset()` -> `Restore()`
