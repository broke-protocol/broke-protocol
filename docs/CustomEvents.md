# CustomEvents

> This example will cover the basics of the ``EventsHandler``. This class is used for a lot of Custom event handling. These act as callbacks for trigger events, custom Entity Actions, and UI Element events.

## What is the ``EventsHandler`` class?
The ``EventsHandler`` class is a class that allows the game and plugins to communicate with each other. Triggers, Entity Actions, and UI Element events can all callback to your Plugin methods tagged with [CustomTarget]. You can look up 'Custom Entity Actions' on the [Modding Guide](https://brokeprotocol.com/modding-guide) for more info.  

In this example we are going to show you how to do the following:
- Register Custom Events & Call them
- Subscribe to existing events

## Registering a custom events
Registering a new custom event is very simple, All you need to do is call the static method ``EventsHandler.Add()``.
```csharp
EventsHandler.Add("ExampleEvent", new Action(() => 
{
  Logger.LogInfo("ExampleEvent got called!"); // The 'Logger' instance is a class from BP-CoreLib. Using 'Debug.Log()' here will work just fine too.
}));
```
Or there's also a shorthand method where you can use the ``[CustomTarget]`` Attribute. Methods with this attribute will be automatically added to the EventsHandler as long as they're defined within an IScript implementation.
```csharp
[CustomTarget]
public void ExampleEvent()
{
  Logger.LogInfo("ExampleEvent got called!");
}
```

## Invoking custom events
```csharp
EventsHandler.Exec(key, arguments);
```
Yup. that's all.
```csharp
ReturnType returnValue = EventsHandler.Get<ReturnType>(eventID, arguments);
```
This if you need a return value from the function.
```csharp
// .. in some IScript implementation
[CustomTarget]
public bool OnExampleEvent(string test)
{
  Debug.Log($"ExampleEvent got called, with the argument test: {test}");
  return test == "ExampleArg";
}

// .. somewhere else
bool isTrue = EventsHandler.Get<bool>("ExampleEvent", "ExampleArg"); // bool with the event return value
```
