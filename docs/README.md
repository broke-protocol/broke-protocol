# Getting Started

## Quickstart
> Before we can get started, you need to have a few programs installed first.
- Visual Studio IDE 2019 (Any IDE would work, but VSIDE makes it easier to create a new library.)
- Broke Protocol installed
- C# Programming knowledge  

![alt text](https://brokeprotocol.com/wp-content/uploads/References.png "References")

1. Create a new class library for .NET Framework version `4.7.2` or start with [GameSource](https://github.com/broke-protocol/broke-protocol) as a basis.
2. Add references to ``UnityEngine.dll``, ``UnityEngine.CoreModule.dll``, ``UnityEngine.PhysicsModule.dll``, and ``Scripts.dll`` from the ``BrokeProtocol_Data/Managed/`` directory. These are the only *required* dll's, but you might need to import more later.
3. Create a new class to implement the ``Plugin`` class. In GameSource, this class is called ``Core.cs``, but you can call it whatever you want.

!> You must have a class that implements the ``Plugin`` class. Without one, your resource will not be loaded in.

```csharp
public class ExamplePlugin : Plugin
{
    public ExamplePlugin()
    {
        // Example Plugin is the name and "ep" is the namespace prefix for command permissions defined in this plugin
        Info = new PluginInfo("Example Plugin", "ep");
    }
}
```

!> ``GroupNamespace`` **must** be set before the resource gets loaded. (Second argument for the ``PluginInfo`` constructor.) Otherwise the resource will not be loaded in.

``PluginInfo`` has other properties you can change, as seen here:
```csharp
Info = new PluginInfo("My Plugin", "mynamespace")
{
    Description = "A simple description",
    Website = "https://mywebsite.com/"
};
```

> ``4`` and ``5`` are optional. Do note if you don't do these steps you need to manually copy your dll every time after build to your broke protocol server folder.  

4. Edit your project (csproj) and go to ``Build Events``
5. Put the following text in ``Post Build Event``
```
copy /Y "$(TargetFileName)" "FULL PATH TO YOUR BROKE PROTOCOL TESTING SERVER Plugins/ FOLDER"
```
Eg:
```
copy /Y "$(TargetFileName)" "D:\BrokeProtocolServer\Plugins"
```
Now every time you build your class library your target file will automatically be copied.
6. Make sure your dll gets copied correctly after building. If everything seems correctly then every time your run your server from that folder your plugin will be loaded in.
7. Once you're ready for the next step, go to the Examples page and start with the first one.

## Tips
- Check the [Official Unity3D Manual](https://docs.unity3d.com/Manual/index.html) for Unity specific documentation.
- Stay away from multi-threading (System.Threading & System.Timers) since much of BP and Unity code is not thread-safe. Use Unity Coroutines or InvokeRepeating instead to keep things on the main thread.
- Don't forget to Upsert changes back into the database when finished editing. Typical usage:
```cs
if(svManager.TryGetUserData(accountID, out var playerData)))
{
    DoWhatever(playerData);
    mannager.database.Users.Upsert(playerData);
}
```
