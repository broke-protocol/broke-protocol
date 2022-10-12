# Events

> This example will cover the basics of the ``EventsHandler``. This class is used for a lot of things including game events, CEF events, and custom events.

## What is the ``EventsHandler`` class?
The ``EventsHandler`` class is a class that allows resources to communicate with each other. CEF UI events, and game events can all callback to your plugin methods tagged with [CustomTarget]. Game events include Map Trigger objects or Custom Entity actions (see 'Custom Entity Actions' on the [Modding Guide](https://brokeprotocol.com/modding-guide)).  

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
Overriding game events is the main way Plugins hook into the game and get their functionality called. The Events classes are all in the BrokeProtocol.API namespace and contain all the virtual methods that can be overridden with your own behavior.

``ManagerEvents`` -> All overarching manager methods, update loops, and events.

These next Events classes are a hierarchy from top to bottom. With subclasses having a superset of methods of the parent class.
``EntityEvents``
``MountableEvents``
``DestroyableEvents``
``PhysicalEvents``
``MovableEvents``
``PlayerEvents``
If you hook into the same event at multiple levels of the hierarchy, know that they are always executed from base classes first. For example if you want to change the spawn location during a Respawn event, the Respawn method exists at every level of the heirarchy. But the actual spawn location is selected at the MovableEvents subclass in GameSource if you look at the code. So you should Override the Respawn method in MovableEvents to change the where the spawn location is.

There's also a special ``Execution`` attribute which will modify how multiple plugins on the same event will react and order themselves with each other. Plugins are always loaded in alphanumeric order, which is why the default plugins start with '!' so they load first. But plugins can either all hook onto the same event or override/disable previously loaded hooks in order to change their behavior according to the following Execution modes.

The ``Execution`` attribute takes one of the following ExecutionModes as arguments:

``[Execution(ExecutionMode.Test)]`` -> Use this for pre-testing conditions before executing the event. Must return bool type.

``[Execution(ExecutionMode.Additive)]`` -> This is the Default Execution mode if no Execution Attribute is used. Adds your hook onto a list of other plugins hooked on the same event.

``[Execution(ExecutionMode.Override)]`` -> Use this to override (disable) any existing Additive or Override hooks on the same event.

``[Execution(ExecutionMode.Event)]`` -> This method cannot be overriden and will always be called on this event (if all Test methods are passed).

``[Execution(ExecutionMode.PostEvent)]`` -> This event will be called after all other Additive, Override, and Event methods. Cannot be overriden.

Additionally, any method at all can return a bool type and if it returns false, any following methods on the same event chain will stop execution. So for example a PostEvent method will not run if any methods in Test/Additive/Override/Event Execution return false.

See the GameSource repo for entire mods written using these hooks and events.
