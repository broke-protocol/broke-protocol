# 02: Events

> This example will cover the basics of the ``EventsHandler``. This class is used for a lot of things including game events, CEF events, and custom events.

## What is the ``EventsHandler`` class?
The ``EventsHandler`` class is a class that allows resources to communicate with each other, CEF events to Plugins, and game events (Map Trigger objects for example) to callback to your Plugins.  
In this example we are going to show you how to do the following:
- Register Custom Events & Call them
- Subscribe to existing events

!> To learn about CEF and it's implementation with the ``EventsHandler``, check the CEF example.

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

## Subscribing to a game event
Subscribing to a game event is quite different. Any method with a ``Target`` Attribute will be automatically added to the chain of subscribers to the event. No need to call any Add function for the event.

Events are listed in ``BrokeProtocol.API.GameSourceEvents`` and the ``Target`` Attribute must have the EventID and ExecutionMode as arguments as such:

``[Target(GameSourceEvent.Example, ExecutionMode.Event)]`` -> This method will always be called on this event

``[Target(GameSourceEvent.Example, ExecutionMode.Override)]`` -> Any further subscribers with ExecutionMode.Override will not be called

```csharp
// Any other plugins targeting PlayerGlobalChatMessage will be executed
[Target(GameSourceEvent.PlayerGlobalChatMessage, ExecutionMode.Event)]
public void OnGlobalChatMessage(ShPlayer player, string message)
{
  if (player.health <= 20f) 
  {
    player.SendChatMessage("No chit chat, you're low on health!");
  }
}

// ExecutionMode.Override -> This is the final stop - Any plugins loaded after (including zGameSource.dll) won't be executed
[Target(GameSourceEvent.ManagerSave, ExecutionMode.Override)]
protected void OnSave(SvManager svManager)
{
    ChatHandler.SendToAll("Saving server status..");
    foreach (ShPlayer player in EntityCollections.Humans)
    {
        player.svPlayer.Save();
    }
    svManager.database.WriteOut();
}
```
