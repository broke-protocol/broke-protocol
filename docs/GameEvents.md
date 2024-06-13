# Game Events
> This example will cover the basics of the Game Events. The way the game communicates important gameplay events to Plugins.

## Subscribing to a game event
Overriding game events is the main way Plugins hook into the game and get their functionality called. The Events classes are all in the BrokeProtocol.API namespace and contain all the virtual methods that can be overridden with your own behavior.

- ``ManagerEvents`` -> All overarching manager methods, update loops, and events.

These next Events classes are a hierarchy from top to bottom. With subclasses having a superset of methods of the parent class.

- ``EntityEvents``
  - ``MountableEvents``
    - ``DestroyableEvents``
      - ``PhysicalEvents``
        - ``MovableEvents``
          - ``PlayerEvents``
          - ``TransportEvents``

If you hook into the same event at multiple levels of the hierarchy, know that they are always executed from base classes first. For example if you want to change the spawn location during a Respawn event, the Respawn method exists at every level of the heirarchy. But the actual spawn location is selected at the MovableEvents subclass in GameSource if you look at the code. So you should Override the Respawn method in MovableEvents to change the where the spawn location is.

There's also a special ``Execution`` attribute which will modify how multiple plugins on the same event will react and order themselves with each other. Plugins are always loaded in alphanumeric order, which is why the default plugins start with '!' so they load first. But plugins can either all hook onto the same event or override/disable previously loaded hooks in order to change their behavior according to the following Execution modes.

The ``Execution`` attribute takes one of the following ExecutionModes as arguments:

- ``[Execution(ExecutionMode.PreEvent)]`` -> Executed before other event types. Use this for pre-testing conditions (if you return an optional bool).
- ``[Execution(ExecutionMode.Additive)]`` -> Default ExecutionMode. Adds your method onto a list of other Plugin methods hooked on the same event.
- ``[Execution(ExecutionMode.Override)]`` -> Override (disable) any existing Additive or Override hooks on the same event.
- ``[Execution(ExecutionMode.Event)]`` -> Will always be called on this event (if no previous methods return false). Cannot be Overridden.
- ``[Execution(ExecutionMode.PostEvent)]`` -> Called after all other Additive, Override, and Event methods. Cannot be overriden.

Additionally, any method at all can return a bool type and if it returns false, any following methods on the same event chain will stop execution. So for example a PostEvent method will not run if any methods in PreEvent/Additive/Override/Event ExecutionModes return false.

See the GameSource repo for entire mods written using these hooks and events.
