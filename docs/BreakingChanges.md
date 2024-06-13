## 1.40
?> This Update should have minor effects on modding. Mostly old/deprecated types and parameters removed. Some World Builder changes mean vehicles should have their ForSale state updated on maps.

### API
* UI ShowText API: Removed some screen anchoring and sizing parameters from old UI system
* Removed custom Callback delegates, now using System.Action/Func
* Moved all Consume code into GameSource event

### MODDING
* Player weight limits are now moddable per character
* FlareEffectiveness is a moddable property of SvThrown entities
* Vehicles now have all 3 States available in the World Builder: Unlocked/Locked/ForSale

## 1.39
?> This Update adds runtime interior cloning and deletion for modding use, and also updates how UIs are cloned and destroyed. Some extra modding functionality on players and equipables added as well.

### API
* Unity physics naming changes with related to velocity and drag (angular and linear)
* ShEntity.Grounded replaced with InWater and Ground Properties
* SceneManager.Instance.ClonePlace()
* SceneManager.Instance.DestroyPlace()
* SvPlayer.VisualTreeAssetClone()
* SvPlayer.VisualElementRemove()

### MODDING
* Custom UIs are no longer constructed automatically
  * Call SvPlayer.VisualTreeAssetClone() to clone custom UIs by name with an optional parent parameter
* Player jump height is now a moddable property
* Equipable Attach Bone is now a moddable property ('RightHand' by default)

## 1.38
?> This Update generally appends onto the API but it does still change some methods outlined below. Most important is the new ShTrain class and physics overhaul that might affect some old mods.

### API
* New ShTrain class for modding
* Multiple ExitTransforms are supported on vehicles (closest to player is selected)
  * Old vehicle mods using the single ExitTransform might need updates (mainly important for boats)
* SvEntity.SvRepositionSelf() -> SvPhysical.SvRelocateSelf()
* ClPacket.RepositionSelf -> RelocateSelf
* UI Elements modding API (TheUnishark)
  * New methods supporting Sliders, Dropdowns, Radio Buttons, Toggles, Etc.
  * Removed one generic param in the query of ClManager.GetVisualElement<T>()
  * Fixed ClManager.GetTextFieldText method was sending the wrong packet
  * Added SvPlayer/ClManager.SetTextElementText(string element, string text)
  * Solved an issue where disabled/invisible elements were blocking UI clicks
* Util.Log() is the recommended method for logging now
* SpawnBot() targetVehicle parameter removed (prefer SvMount() after spawn)
* CanSee(Vector3 direction, float distance) => CanSee(Vector3 position)

### MODDING
* Dynamic Towing LineRenderer is supported, see TowTrucks in BPResources for how to setup
* mobsUpdate backwards compatibility hack removed
  * Old mods should update their seating position since animal mobs were added
* exitTransforms is now an array supporting multiple exits
  * Closest exit is picked
  * If none defined, a fallback way of picking a safe exit is used
  * Mods relying on this (like boats and big aircraft) should be rebuilt
* New Torque parameter
  * Defines power falloff at high end of speeed
  * New default value is a 'middle-ground' and can be tweaked
  * Old mods might need updating
    * Old surface vehicle mods will be more powerful
    * Old air vehicle mods will be less powerful 

## 1.37
?> Most API changes in the Truck Simulator Update are related to the new Towing functionality in 1.37. This includes a new TowT transform reference and a list of TowOptions on each vehicle for random spawning. Important to note an overall simplification to accessing specific Mount types and Controllers since it was getting confusing with all the different Properties. Also in continuing to simplify the class heirarchy, ShWheeled class is merged into parent ShTransport so even boats and planes can have animated wheels, skidmarks, particles, etc. This opens to door to amphibious vehicles later on and potentially better wheel damage/blowout modelling too.

### API
* ShWheeled class removed (all Transports can use wheel skidding particles/audio/models)
* Damage event source parameter replaced with hitNormal
* New PlayerTow event added
* IsDriving, IsFlying, IsBoating, IsDragging, etc replaced with generic IsMount<T>(out T)
* 'Controller' virtual property removed, access 'controller' field directly
* OutsideController property only available on ShPlayer now
* GetSectorFloor moved from SvManager to Util static class
* CanSpawn() moved up to ShEntity and now takes parameter ShEntity[] ignoreArray
* SvPlayer.StartLocking() -> StartLockOn() to distingish from vehicle locking functions

### MODDING
* All Aircraft and Boats mods need these parameters multiplied by 0.04 (divide by 25) and re-exported due to physics fixes and updates
   * Boats:
      * engineFactor
      * turnFactor
      * stabilityFactor
   * Aircraft:
      * uprightStrength
      * stabilityStrength
   * You can simply add "/25" after each number in the inspector and Unity will calculate the result when you Enter or Tab out

## 1.36
?> The UI Update adds events related to changing ChatMode and ChatChannel so the server can override or block that behavior. Also, some old client packets were removed to prevent misuse (by me). But the gap in the ClPacket enum changed a bunch of packet IDs so any plugin sending packets directly (SvPlayer.Send(...)) will likely need rebuilding.

### API
* Removed old SerializedAttachments/Wearables client packets
* Modded Underbarrel Attachments can adjust default 'Setting'
* SetChatChannel and SetChatMode events added to GameSource

## 1.35
?> The Destruction Update adds a new class of events for Voxels. Also there's a public API on the new ShVoxel class if you want to manipulate them at runtime. Chat handling is also changed to account for a new ChatMode set for each player that decides whether their voice/message will go Public, Job, or Private Channel when using LocalChat.

### API
* New Voxel event class added (More events coming later)
* Fixed some missing namespaces
* Use HashSet for command lookups
* Mount event implementation moved to GameSource
* Some ShGun parameters/functions moved up to ShWeapon base class
* Global/LocalChatMessage events & packets renamed to ChatGlobal/Local
* New ChatVoice event added
* New handling for ChatLocal messages depending on SvPlayer.chatMode
* SvPlayer.ResetZoom renamed to ResetMode
* Net serializer now supports ushort, int3, and Color32 for new features
* PlaceItemCount moved to SceneManager
* Heightmaps data update ('pngBytes'->'heightmapData' to update old maps)
* New 'Always Visible' Entity parameter (entities always loaded on clients)
* SvEntity.Despawn() renamed to Deactivate() for consistency
* GameSource fix for dead NPCs stuck in vehicles

## 1.3
?> The War Update splits the game up into 3 different plugins. GameSource now contains only core logic related to connections, AI, damage, inventories, etc. LifeSource contains everything related to crimes, jails, RP Jobs, and random AI traffic spawning. The new WarSource plugin adds a new territory capture game mode, new login flow, and AI that can battle in team-based combat. Each plugin uses it's own Extended Player class but all its methods are virtual. You can extend these classes in your own plugin or override their existing behavior. If you want to change behavior or add your own hooks to those methods, make sure you Override the PlayerInitialize event and insert your own class into the pluginPlayer dictionary.
?? Also new are the 'Events' abstract classes. These are ManagerEvents, EntityEvents, MountableEvents, DestroyableEvents, PhysicalEvents, MovableEvents, and PlayerEvents. Override methods in each class to add your own behavior instead of using the old [Target] attributes. This should help you see what's available for modding and their exact parameters. You can use the new [Execution] attribute to change how the methods are called.

### API
* All core game logic moved to GameSource
* All crime, jail, and police handling moved to LifeSource
* Dropping items sets the 'spawner' field on entities (for modding)
* All object types have a Data field now (use in World Builder and in API)
* Data string field restored after entity respawns (Thanks @Olivrrr)
* Player Maintenance loop moved to LifeSource
* New CustomPacket event for pre-login UI handling
* API: [Target] attribute deprecated, Override new Events classes for custom handling
* All Managers and World classes are now Singletons with Instance accessors
* Manager reference no longer passed to Manager events (use Singleton classes)
* Null strings are now handled by the network serializer as empty strings
* BPAPI changed to static class
* GroupManager -> GroupHandler for consistency with other static classes
* ChatHandler -> InterfaceHandler
* BrokeProtocol.API.Types namespace removed
* Added SetMaxSpeed() to ShMovable class (for modding)
* Added ExecutionMode.Additive (similar to Override except overridden events are still executed)
* Removed ExecutionMode.Final (easy to abuse/misuse)
* Added customData field to ConnectionData for pre-join modding
* Removed GroupIndex.Gangster (just use 'Criminal' now)
* API: Must manually call SvDestroyMenu(id) (except when showing a new menu with the same ID)
* All events can now return a boolean to stop execution further execution on that same event chain
* Connection related data like deviceID and passwordHash moved to svPlayer.connectData
* Stop all entity coroutines immediately on destruction (prevent some race conditions)

### Mapping
* Use the new Territory entity to replace the old 3 Territory types
    * Set Owner Index field to jobIndex that you want to give initial ownership
    * Any Owner Index less than 0 or greater that job Count will be set to unowned
    * Job indices for the 3 gangs in LifeSource are 6, 7, 8
    * Scale of territories in Default are 200x200x200
* Use new ServerTriggers to replace ThrowableTarget, RestrictedArea, AreaWarning, Repair, Rearm, etc
    * Add the event name in Enter/Exit Event field in the World Builder ("RestrictedArea" etc. in GameSource)
    * 1.3 backwards compatible objects will keep your old Trigger entity locations/transforms
      * But they will be missing the event names, so don't forget to add them
* To make a War map
    * You just need to place Territories (these are used for spawning and Spawn objects are ignored)
    * You can move, scale, or rotate territoroes, just be mindful of spawning and bot navigation
    * It helps the AI if you place simple Boat and Aircraft waypoints around the map (see DefaultWar)

## 1.25
?> The Locked On Update overhauls how thrown items are handled for projectiles and vehicles. Now they offer multiple weapon sets that can be cycled through so old mods will have to be updated for their weapons to be usable again. Also some hideInterior references for hiding tank turrets, etc is now per-seat instead of per-vehicle. For code changes, GameSource.dll has been renamed to load first and ExecutionModes have been altered slightly though it shouldn't break things in the majority of cases.

### API
- New Plugin ExectionModes
    - ExecutionMode.Override events will replace previous override events
    - Added ExecutionMode.Final (Doesn't allow any more overrides, same as Override behavior prior to 1.25)
- ShowAlert packet added to API
    - Use helper functions for SvShowTime and SvShowAlert now instead of sending packets manually
- New svManager.Add/RemoveInventoryAction() for custom inventory item actions/events
- Added ShEntity.CenterBounds/Mass properties for aiming/looking/buoyancy
- svEntity.thrower -> svEntity.instigator
    - Since it's used for fires, exited vehicles, more than just thrown items

### MODDING
- Boats now have a stabilityFactor moddable property that was hard-coded before
- Physics updates -> Mods likely need values updated:
    - Aircraft mods should multiply old values by 25x : uprightStrength, stabilityStrength
    - Boat mods should mutltiply old values by 25x : engineFactor, turnFactor
- Steerable class removed
    - Modders delete Steerable scripts in Unity project if updating
- HideInterior is now a per-seat property
    - Mods using this to hide player models need to update
- ThrownEntities for Projectiles and Vehicles must be redone under new Weapon Sets property
- New Target Type property on Thrown entities makes the projectile locking/tracking any entity of that type
- New 'Flare' tag will make any Entity act as countermeasures for guided weapons

## 1.22
?> The Cracking Update has a few important breaking changes but mostly extends the API with additional functionality around the lockpicking minigame and almos mounting and dismounting vehicles

### API
- GameSource New Events
    - PlayerCrackStart, PlayerMount, PlayerDismount
- GameSource OnDamage Events now receive more useful parameters about the source
    - New parameters Vector3 source, Vector3 hitPoint
    - Replaces old hitY parameter
    - Used for damage calculations and also for direction markers
- New 'inventoryType' Enum
    - Replaces hasInventory, shop, lockable fields
    - Removed 'Safe' Class of items (use Entity with Locked inventoryType)
- New entity methods
    - HasInventory
    - Shop
    - InApartment
    - CanView
    - CanCrack
- Removed svManager.fixedPlaces (due to cleaner InApartment implementation)
- Removed svManager.payScale (handled fully in GameSource now)
- Removed BrokeProtocol.Prefabs namespace (just use BrokeProtocol.Entities)
- New LineGraphic, QuadGraphic, CircleGraphic UI Classes
    - Available for ExecuteCS but will have proper API later

### MODDING
- All Transports have some air control now
    - Adjust with new per-axis orientStrength Property
- Thrown objects now support a customDestroyEffect
    - Old mods need reconfiguration
- Hitscan items now support a customFireEffect
- Added vanilla game Destructibles to BPResources
    - Working and properly modable now
