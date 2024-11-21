## 1.43
?> Significand API changes are outlined below but older methods will still be availabe as deprecated where possible. Asset modding has new proprty for lights object which must be updated.

### API
- Environment packet has new values for light intensities
- Deprecated and Renamed Methods
  - ShEntity.GetPosition -> Serialized.Position
  - ShEntity.GetRotation -> Serialized.Rotation
  - ShEntity.GetRotationT -> Serialized.RotationT
  - ShEntity.GetOrigin -> ShEntity.Origin
  - ShEntity.GetPlace -> Serialized.Place
  - ShEntity.GetParent -> Serialized.Parent
  - ShPhysical.GetStanceIndex -> ShPhysical.StanceIndex
  - Serialized.GetChildIndex -> Serialized.ChildIndex
  - Place.GetIndex -> Place.Index
  - .GetEntranceDoor -> Place.EntranceDoor
- Renamed Methods
  - ShEntity.GetPlaceIndex -> Serialized.GetPlaceIndex()
  - SvEntity.GetDoor -> SvEntity.GetDoor()
  - ShPlayer.GetControlled -> ShPlayer.GetControlled()
  - ShPlayer.GetMount -> ShPlayer.GetMount()
  - ShItem.GetSortableName -> ShItem.SortableName
  - SvManager.GetMainGraph -> SvManager.MainGraph
  - Serialized.DirectionIndex() -> Serialized.GetDirectionIndex()
  - SceneManager.PlaceItemCount() -> Place.GetItemCount()
  - CustomData class rewritten with new utility methods
  - Old methods still available as Deprecated 

### MODDING
- lightsObject property moved from ClTransport to ShTransport for universal day/night handling
- Maps likely need to be updated with proper night lighting now
  - Or use environment/lighting commands

## 1.42
?> New moddable HUD and other UI assets in BPResources. New way to Bind Cursor to UI elements for automatic cleanup. Most other 'Sector' related API changes are due to addition of level streaming but shouldn't affect too many plugins.

### API
- Default menus will try to clone Custom UIs with the same UXML name if available
- svPlayer.VisualElementCursorVisibility(string elementName)
  - Binds cursor visibility to a VisualElement
  - Disables Cursor automatically when element destroyed
- SvPlayer.randomSpawn moved to GameSourceEntity type
- EntityCollections.RandomNPC returns any NPC now, not just randomSpawns
- Added SvPlayer.ResetPath() to clear pathing data
- Added SvPlayer.LookTarget() for aiming/shooting at target separate from movement
- SvManager.Instance.AddNewEntityExisting(..)
  - respawnable parameter no longer optional
- SvPlayer.PlayerData removed
  - Use SvPlayer.CustomData directly
- Sector -> NetSector
- sectorRange -> netSectorRange
- visibleRange -> netVisibleRange
- visibleRangeSqr -> netVisibleRangeSqr
- manhattanRange -> netPerpendicularRange
- Some Sector methods removed
  - Reimplemented in GameSource where needed

### MODDING
- New Destructible Effects supported
  - Custom Destroy Effects can be set similar to thrown Destroy effects

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
* Added ShPlayer.OnFoot property (true for InWater too)

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
