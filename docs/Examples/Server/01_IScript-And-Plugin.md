# 01: IScript and Plugin

> These are the core classes and interfaces for plugins. These can be used to automatically instanciate classes, and the main entry point of the plugin.

## IScript

_What is `IScript`?_

`IScript` is a (empty) interface used to instanciate any class within the plugin dll. It will enumerate over all types within the dll and invoke all types that implement the `IScript` interface. So for each type you want to automatically instanciate, use `IScript`.

Take the following for example:

```csharp
using System;
using UnityEngine;

namespace ExampleNamespace
{
  public class ExampleClass
  {
    public ExampleClass()
    {
      Debug.Log("ExampleClass .ctor got invoked");
    }
  }
}
```

With only this (and a `Plugin` class, which I will explain after this), you'll get the following output:  
```

```
Yup, nothing. That's why we added `IScript`. You don't need to do anything, because all `IScript` implementations will get called at runtime.

The following will work just fine:

```csharp
using System;
using UnityEngine;
using BrokeProtocol.API;

namespace ExampleNamespace
{
  public class ExampleClass : IScript // Notice the IScript here
  {
    public ExampleClass()
    {
      Debug.Log("ExampleClass .ctor got invoked");
    }
  }
}
```
This will output: `ExampleClass .ctor got invoked`, as expected.  
:tada: Now, you may ask; how is this useful?  
Well, take for example the following:
```csharp
using System;
using UnityEngine;
using BrokeProtocol.API;
using BrokeProtocol;

namespace ExampleNamespace
{
  public class ExampleClass
  {
    public ExampleClass()
    {
      GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnIntialize, new Action<ShPlayer>(OnEvent));
    }
    
    public void OnEvent(ShPlayer player)
    {
      player.SendChatMessage("Welcome to the server!");
    }
  }
}
```

Now within your `Plugin` class, you need to create a instance of this class:

```csharp
// Your Plugin class
public ExampleClass ExampleClass { get; set; }

public ExamplePlugin()
{
  ExampleClass = new ExampleClass();
}
```

This is fine for just one class, but when you get a lot of classes, this can get really messy. That's why `IScript` is useful, so that the full code will be:
```csharp
using System;
using UnityEngine;
using BrokeProtocol.API;
using BrokeProtocol;

namespace ExampleNamespace
{
  public class ExampleClass : IScript
  {
    public ExampleClass Instance { get; private set; }

    public ExampleClass()
    {
      if (Instance == null)
      {
        Instance = this;
      }
      GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnIntialize, new Action<ShPlayer>(OnEvent));
    }
    
    public void OnEvent(ShPlayer player)
    {
      player.SendChatMessage("Welcome to the server!");
    }
  }
}

// Your plugin class
public ExampleClass ExampleClass { get; set; }

public ExamplePlugin()
{
  ExampleClass = ExampleNamespace.ExampleClass.Instance;
}
```

Yeah, that looks better. Now, in most cases you don't need a reference to the type, as they should be self contained. When that's the case, you can just only set the type like so:
```csharp
using System;
using UnityEngine;
using BrokeProtocol.API;
using BrokeProtocol;

namespace ExampleNamespace
{
  public class ExampleClass : IScript
  {
    public ExampleClass()
    {
      GameSourceHandler.Add(BrokeProtocol.API.Events.Player.OnIntialize, new Action<ShPlayer>(OnEvent));
    }
    
    public void OnEvent(ShPlayer player)
    {
      player.SendChatMessage("Welcome to the server!");
    }
  }
}
```

:tada:! Cool, so let's recap:
- `IScript` Is an type used to instanciate classes.
- `IScript` Can be used to make self contained classes and have no external instanciations.
- `IScript` **must** have a `public`, parameterless constructor.
- Your type must be contained within the dll of the `Plugin` implementation.

Now, onto the `Plugin` class.

## Plugin

_What is a `Plugin` class?_

The `Plugin` will be used as plugin entry point. **This class is required.** Some of the fields within the `Info` property will be used by various internal types, for example the `CommandHandler` and `GroupManager`.

The most basic example would be the following:

```csharp
public class Core : Plugin // Notice the Plugin here
{
    public Core()
    {
        Info = new PluginInfo("ExamplePlugin", "er");
    }
}
```

This will be invoked at runtime. The following constructors are available:
```csharp
public PluginInfo(string name, string groupNamespace)
```
```csharp
public PluginInfo(string name, string groupNamespace, PluginAuthor author) : this(name, groupNamespace)
```
```csharp
public PluginInfo(string name, string groupNamespace, List<PluginAuthor> authors) : this(name, groupNamespace)
```

`Name` is pretty straightforward. This is the `Plugin` name.
`GroupNamespace` is used within the `GroupManager`. This will be explained in depth in another example.
`Authors` is a list of plugin authors. You can set the author name and author function here.

Normally the class that implements the `Plugin` class is named `Core`. **This is not an requirement**. This just makes it clearer to understand for other users reading your codebase.

Now the `PluginInfo` instance exposes a few more properties, as shown here:
```csharp
public Core()
{
    Info = new PluginInfo("ExamplePlugin", "eq")
    {
        Description = "A descrption for the plugin. This is optional, but recommended",
        Git = "https://github.com/a-github-or-equivalent-link"
    };
    RegisterEvents();
}
```

Now at the time of writing, we got a few more properties available at your disposal:
```csharp
string Name
string GroupNamespace
string TargetVersion 
string Description
string Website
string Git
List<PluginAuthor> Authors
```

All but `TargetVersion` are not directly used by the API. The `TargetVersion` is used to set the target version of the **game**, so when that doesn't match it won't load in the plugin.

There is also a `ToString` override which outputs `Name: Description` or `Name` if `Description` is null.

---

This should be all you need to know to create a simple plugin. Using this knowledge, try to make a plugin that prints a message to the chat when someone joins, and when someone leaves.