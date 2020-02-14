# 02: CEF UI Rendering

CEF refers to the [Chromium Embeded Framework](https://en.wikipedia.org/wiki/Chromium_Embedded_Framework).
It's basically an open-source framework for embedding a browser within the Broke Protocol client. We integrated
this browser framework instead of creating something from scratch because Chromium is robust, flexible,
well-established, and (generally) performant.

CEF runs in the background on a separate process: SharedPluginServer.exe. You might see multiple instances of 
this process but those are just the rendering process, browser process, etc, for better core utilization.

You can create game interfaces using HTML, CSS, and JavaScript just like you would create any web app or
interface. There are some injected methods explained in [01 - Getting Started] that help you connect with the
game client and even server (via triggers).

There are also function on the server that let you execute either JavaScript or C# on the client. Don't need to
worry since the code is validated to be safe on clients before execution. This is a useful way to change UI
elements dynamically based on server events, or even change client state directly via C#.

SvPlayer.ExecuteJS(string expression)
ex. svPlayer.ExecuteJS("window.cursorHide();")

SvPlayer.ExecuteCS(string expression)
ex. svPlayer.ExecuteCS("clManager.AllowHUD = false;")