# UI Elements for Client UI

UIElements is a flexible new feature in Unity that allows custom UIs to be displayed, styled, and linked to events on the server in Broke Protocol.
Creating these UI's involves Unity's built-in UI Builder and the workflow is very similar to html/css (called uxml/uss in Unity).

You can find tutorials and docs on Google and Youtube. There are also some example assets (wanted_stars, server_logo_example, and war_score) in BPResources.
Exporting these UIs is very similar to exporting normal asset BPA's (just mark the .uxml asset as an AssetBundle in Unity and go to `Window -> Broke Protocol Asset Export`

I suggest setting your top-level element Canvas Size to 1280x720 so it matches the base game resolution exactly. With this setup, you can do all your sizing/margins/padding in pixels since the game will scale everything automatically according to in-game resolution.
![alt text](https://brokeprotocol.com/wp-content/uploads/CanvasSize.png "Canvas Size")

The following UI methods exist on SvPlayer:
* public void VisualTreeAssetClone(string visualTreeAssetName, string parentVisualElementName = "", string newVisualElementName = "")
* public void VisualElementRemove(string element)
* public void VisualElementOpacity(string element, float setting)
* public void VisualElementDisplay(string element, bool setting)
* public void VisualElementVisibility(string element, bool setting)
* public void VisualElementOverflow(string element, bool setting)
* public void AddButtonClickedEvent(string element, string eventName)
* public void GetTextFieldText(string element, string eventName)
* public void SetTextElementText(string element, string text)
* public void GetSliderValue(string element, string eventName)
* public void SetSliderValue(string element, float value)
* public void SetProgressBarValue(string element, float value)
* public void GetToggleValue(string element, string eventName)
* public void SetToggleValue(string element, bool value)
* public void GetRadioButtonGroupValue(string element, string eventName)
* public void SetRadioButtonGroupValue(string element, int value)
* public void SetRadioButtonGroupChoices(string element, List<string> choices)
* public void GetDropdownFieldValue(string element, string eventName)
* public void SetDropdownFieldValue(string element, int value)
* public void SetDropdownFieldChoices(string element, List<string> choices)

The most important are `VisualTreeAssetClone` and `VisualElementRemove` which instantiate and remove UI elements respectively.

?> Modders can now use complex paths to Get/Set VisualElements using the UI API methods. Similar (but not identical) to `querySelector()` in JavaScript.
* “parent/descendant..” for descendants
* “parent>child..” for immediate children
* ".class" selectors supported now
* Can combine descendant/child/name/class lookups in a path

`VisualTreeAssetClone` should use the uxml Asset name to clone the element with optional parameters to place under a parent and rename the cloned element (for easier lookup later).

`VisualElementRemove` will remove the element from the client UI. Note, if multiple elements share the same name, only the first one in the document tree will be removed. So it might be useful to use the `newVisualElementName` parameter from `VisualTreeAssetClone` to give each tree clone a custom name.

`AddButtonClickedEvent` binds a UI Button element with a plugin event. The event in your plugin must have a signature that looks like:

```cs
[CustomTarget]
 public void YourButtonClickHandler(ShPlayer sender, string elementName)
{}
```
So you would call `player.svPlayer.AddButtonClickedEvent("ButtonName", "YourButtonClickHandler");` after cloning a visual asset tree on the player.
