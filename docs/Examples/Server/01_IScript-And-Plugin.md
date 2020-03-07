# 01: IScript and Plugin

> These are the core classes and interfaces for plugins. These can be used to automatically instantiate classes, and the main entry point of the plugin.

## IScript

_What is `IScript`?_

`IScript` is an (empty) interface used to instantiate any class within the plugin dll. It will enumerate over all types within the dll and invoke all types that implement the `IScript` interface. So for each type you want to automatically instantiate, use `IScript`.

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
Yup, nothing. That's why we added `IScript`. You don't need to do anything, because all `IScript` constructors will get called at runtime.

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
Well, take for example the following simplified example where you want to set up a singleton:
```csharp
using System;
using UnityEngine;

namespace ExampleNamespace
{
  public class ExampleClass
  {
    public static ExampleClass Instance { get; private set; }

    public ExampleClass()
    {
      if (Instance == null)
      {
        Instance = this;
      }
      Debug.Log("ExampleClass .ctor got invoked");
    }
  }
}
```

You would normally need to instantiate this class to call the constructor within your `Plugin` class:

```csharp
using BrokeProtocol.API;

public class ExamplePlugin : Plugin
{
    public ExamplePlugin()
    {
        ExampleClass example = new ExampleClass();
    }
}
```

This is fine for just one class, but when you get a lot of classes, this can get really messy. That's why `IScript` is useful, so that you don't need to manually call your class constructors individually.

:tada:! Cool, so let's recap:
- `IScript` Is an type used to instantiate classes.
- `IScript` Can be used to make self-contained classes and have no external instantiations.
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

This will be invoked at runtime with the following constructor:
```csharp
public PluginInfo(string name, string groupNamespace)
```

`Name` is pretty straightforward. This is the `Plugin` name.
`GroupNamespace` is used within the `GroupManager`. This will be explained in depth in another example.
Please note that the dlls you place in the `Plugins` directory will be loaded in order by filename. Not the PluginInfo name here.
Only one subscriber per event is allowed to prevent conflicting plugins and simpler return values, so authors must tune their plugin loading order with care.

Normally the class that implements the `Plugin` class is named `Core`. **This is not an requirement**. This just makes it clearer to understand for other users reading your codebase.

Now the `PluginInfo` instance exposes a few more properties, as shown here:
```csharp
public Core()
{
    Info = new PluginInfo("ExamplePlugin", "eq")
    {
        Description = "A descrption for the plugin. This is optional, but recommended",
        Website = "https://github.com/a-github-or-equivalent-link"
    };
    RegisterEvents();
}
```

Now at the time of writing, we got a few more properties available at your disposal:
```csharp
string Name
string GroupNamespace
string Description
string Website
```

Note that `GroupNamespace` is used for permissions. Any events here will use `GroupNamespace` as your prefix for permissions in the `groups.json` file. See the [Group Manager Docs](/Examples/Server/05_GroupManager#permissions) for more info.

There is also a `ToString` override which outputs `Name`.

---

This should be all you need to know to create a simple plugin. Using this knowledge, try to make a plugin that prints a message to the chat when someone joins, and when someone leaves.
