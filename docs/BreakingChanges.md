## 1.41
?> Modders can now use complex paths to Get/Set VisualElements using the UI API methods. Similar (but not identical) to querySelector() in JavaScript.

### API
* VisualElement API functions accept complex paths for element lookups now
	* “parent/descendant..” for descendants
	* “parent>child..” for immediate children
	* ".class" selectors supported now
	* Can combine descendant/child/name/class lookups in a path
* Moved some DamageSource / DamageSourceMap types to GameSource
* ShGun.reloading now public
* Added ShPlayer.OnFoot property (true for InWater too

### MODDING
* Remove any previous Newtonsoft Json Unity Package (new BPResources has one)
* Remove old AssetBundleBrowser directory (new BPResources has V3)

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
