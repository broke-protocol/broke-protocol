<p align="center">
    <a href="http://brokeprotocol.com/">
        <img src="https://brokeprotocol.com/wp-content/uploads/Title.jpg" height="130" alt="Broke Protocol Website"></a>
</p>
<p align="center">
    <a href="http://store.steampowered.com/app/696370/BROKE_PROTOCOL_Online_City_RPG/">
        <img src="https://img.shields.io/badge/Steam-$4.99USD-7289DA.svg" alt="Download the game"></a>
    <a href="https://github.com/broke-protocol/issues/issues">
        <img src="https://img.shields.io/badge/Post-Bugs and Suggestions-7289DA.svg" alt="Bugs and Suggestions"></a>
    <a href="https://github.com/broke-protocol/wiki/wiki">
        <img src="https://img.shields.io/badge/Docs-Wiki-7289DA.svg" alt="Read the wiki"></a>
    <a href="https://discord.gg/WpdNCDw">
        <img src="https://discordapp.com/api/guilds/312436123278376961/embed.png" alt="Chat on Discord"></a>
</p>

# GameSource

### What is this?
This repo is a collection of methods in BP which are moddable by replacing this DLL. Instead of subscribing to the event, you can fully change how the event works. Only the most useful methods will be exposed to this API, more info about that [here](#exposed-methods).


### Installation
1. Fork/Clone/Download repository.
2. Open the just downloaded/cloned repository.
3. Open the `src/BP-GameSource.sln` file using your favorite C# editor. (Suggested version is currently `VS IDE 2019`)
4. Resolve references wherever needed.
5. Make changes where needed.
6. Rebuild source code, and copy + replace the `.dll` from `YourServerFolder/Resources/GameSource.dll` with your newly compiled dll.
7. Done! When you run your server it will now invoke that dll.

### Tips
1. Name your dll `pre_GameSource.dll` to inject it **infront** of the GameSource. Current invoke list is as follows:
  1. `pre_GameSource.dll`
  2. `GameSource.dll`
  3. [A-z].dll
2. Try to keep this dll alone as much as possible. This should only be used as a last option.
3. Because `GameSource` will be loaded in just like any other `Resource`, you'll have to create a `Core` class which implements the `Resource` class. Because of this, you can use a lot of features like Group Manager, Command Handler, etc all within the GameSource. (This is not recommended, but still possible)

### Exposed Methods
Please check [this](https://github.com/broke-protocol/source/blob/master/src/BP-GameSource/Types/) directory for more info.
