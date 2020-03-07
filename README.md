<p align="center">
    <a href="https://brokeprotocol.com/">
        <img src="https://brokeprotocol.com/wp-content/uploads/Title.jpg" height="130" alt="Broke Protocol Website"></a>
</p>
<p align="center">
    <a href="https://store.steampowered.com/app/696370/BROKE_PROTOCOL_Online_City_RPG/">
        <img src="https://img.shields.io/badge/Steam-$4.99USD-7289DA.svg" alt="Download the game"></a>
    <a href="https://github.com/broke-protocol/broke-protocol/issues">
        <img src="https://img.shields.io/badge/Post-Bugs and Suggestions-7289DA.svg" alt="Bugs and Suggestions"></a>
    <a href="https://github.com/broke-protocol/broke-protocol/wiki">
        <img src="https://img.shields.io/badge/Docs-Wiki-7289DA.svg" alt="Read the wiki"></a>
    <a href="https://discord.gg/WpdNCDw">
        <img src="https://discordapp.com/api/guilds/312436123278376961/embed.png" alt="Chat on Discord"></a>
    <br>
    <a href="https://broke-protocol.github.io/broke-protocol"><font size="6">Official Documentation</font></a>
</p>

# GameSource

### What is this?
This repo is a collection of methods in BP which are moddable by replacing this DLL. Instead of subscribing to the event, you can fully change how the event works. Only the most useful methods will be exposed to this API, more info about that [here](#exposed-methods).


### Installation
1. Fork/Clone/Download repository.
2. Open the just downloaded/cloned repository.
3. Open the `BP-GameSource.sln` file using your favorite C# editor. (Suggested version is currently `VS IDE 2019`)
4. Resolve references wherever needed. The `BrokeProtocol.*` namespace requires referencing Scripts.dll from the `BrokeProtocol_Data/Managed/` directory.
5. Make changes where needed.
6. Rebuild source code, and copy + replace the `.dll` from `YourServerFolder/Plugins/GameSource.dll` with your newly compiled dll.
7. Done! When you run your server it will now invoke that dll.

### Exposed Methods
Please check the [Offical API Reference](https://brokeprotocol.com/api/) for more info.
