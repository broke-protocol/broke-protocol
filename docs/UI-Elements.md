# UI Elements for Client UI

UIElements is a flexible new feature in Unity that allows custom UIs to be displayed, styled, and linked to events on the server in Broke Protocol.
Creating these UI's involves Unity's built-in UI Builder and the workflow is very similar to html/css (called uxml/uss in Unity).

You can find tutorials and docs on Google and Youtube. There are also some example assets (WantedStars and ServerLogoExample) in BPResources.
Exporting these UIs is very similar to exporting normal asset BPA's (just mark the .uxml asset as an AssetBundle in Unity and go to `Window -> Broke Protocol Asset Export`

Make sure your PanelSettings on your UIDocument in Unity look similar to the following image or you'll likely see the UI rendered differently in game clients.
![alt text](https://brokeprotocol.com/wp-content/uploads/PanelSettings.png "Panel Settings")

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

The most important are VisualTreeAssetClone and VisualElementRemove which instantiate and remove UI elements respectively.

VisualTreeAssetClone should use the uxml Asset name to clone the element with optional parameters to place under a parent and rename the cloned element (for easier lookup later).

VisualElementRemove will remove the element from the client UI. Note, if multiple elements share the same name, only the first one in the document tree will be removed. So it might be useful to use the `newVisualElementName` parameter from `VisualTreeAssetClone` to give each tree clone a custom name.

AddButtonClickedEvent binds a UI Button element with a plugin event. The event in your plugin must have a signature that looks like:

```cs
[CustomTarget]
 public void YourButtonClickHandler(ShPlayer sender, string elementName)
{}
```
So you would call `player.svPlayer.AddButtonClickedEvent("ButtonName", "YourButtonClickHandler");` after cloning a visual asset tree on the player.
