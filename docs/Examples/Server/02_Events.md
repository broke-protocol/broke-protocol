# 02: Events

> This example will cover the basics of the ``EventsHandler``. This class is used for a lot of things including game events, CEF events, and custom events.

## What is the ``EventsHandler`` class?
The ``EventsHandler`` class is a class that allows resources to communicate with eachother, CEF events to be sent from cef to resource, and game events to be handled with.  
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

```csharp
EventsHandler.Add("ExampleEvent", new Action(OnExampleEvent));

public void OnExampleEvent()
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
ReturnType returnValue = EventsHandler.Get<ReturnType>(key, arguments);
```
This if you need a return value from the function.
```csharp
EventsHandler.Add("ExampleEvent", new Func<string, bool>(OnExampleEvent));

public bool OnExampleEvent(string test)
{
  Logger.LogInfo($"ExampleEvent got called, with the argument test: {test}");
  return test == "UserR00T";
}


// ... somewhere else
EventsHandler.Exec("ExampleEvent", "UserR00T"); // object[] with the return values of all event subscribers
```

## Subscribing to a game event
Subscribing to a game event is almost the same as registering a custom one. You need to grab the value from the ``BrokeProtocol.API.Events`` namespace, and use `GameSourceHandler`.
```csharp
GameSourceHandler.Add(GameSourceEvents.PlayerGlobalChatMessage, new Action<ShPlayer, string>(OnGlobalChatMessage));

public void OnGlobalChatMessage(ShPlayer player, string message)
{
  if (player.health <= 20f) 
  {
    player.SendChatMessage("No chit chat, you're low on health!");
  }
}
```

!> You cannot block execution of the event by changing it to a `Func`. If you need to do that take a look at the GameSource, and modify that.
