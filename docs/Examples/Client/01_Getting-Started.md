# 01: Getting Started

> This example will have everything you need to get started modding the client side. In the next example more things will be covered in depth.


<!-- panels:start -->
### Requirements
- A basic HTML + CSS Understanding
- And all requirements mentioned [here](/README#Quickstart).

### Creating a CEF Page
To make your own CEF UI for client modding, open up your favorite editor. I personally suggest VSC (Visual Studio Code) for web development.

<!-- div:left-panel -->
2. Go to your `/cef/` folder. This should be in `YourServerFolder/www/cef/`. Create a new file in there called `index.html`.
3. Insert boilerplate HTML in there. See the note at the right if you have VSC installed. Otherwise you can copy and paste this:

<!-- div:right-panel -->
> If you use VSC and have the "Emmet" extension installed, you can type <kbd>!</kbd> and then press <kbd>enter</kbd> to auto insert a HTML template.

<!-- div:left-panel -->
```
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1">
    <title>Cef Window</title>
</head>
<body>
    
</body>
</html>
```
Now within this file, you're going to write your HTML content. For this example, I'll write a simple "Hello World" h1 element.
```
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1">
    <title>Cef Window</title>
</head>
<body>
  <h1>Hello World</h1>
</body>
</html>
```
Save (CTRL+S) and open this file, and you should see something like this:
![](https://i.imgur.com/DH3wsLn.png)
<!-- panels:end -->
<!-- panels:start -->
<!-- div:left-panel -->
Yeah. Those are just the basics of course. From now on a bit of HTML knowledge is required. There are plenty of good coures online, on the right side there will be a few coures listed.  

Now try displaying the username onto the screen using `getAsync` and `document.getElementById` (or even better, a reactive framework like Vue or React.)
<!-- div:right-panel -->
- [UDemy](https://www.udemy.com/topic/html/)
- [Codecademy](https://www.codecademy.com/learn/learn-html)
- [Learn HTML](https://www.learn-html.org/)

<!-- panels:end -->

<!-- panels:start -->
### window.`[]`
?> For UI rendering and client-side exectution, the game relies on HTML and JavaScript on the server rendered on the client via CEF.
Everything on the client side is done using CEF. This also includes modifying the game code using functions injected into the JavaScript window object. These are all the functions defined inside `window`.


<!-- div:title-panel -->
## trigger `string eventHandlerName, object arguments`

<!-- div:left-panel -->
> Sends an EventHandler trigger to the Server. Executes an EventHandler trigger subscriber defined in any of your Server Plugins. Always passes the following two arguments to C#: `ShPlayer, JToken`. If no arguments were provided, `JToken` would be `null`.

**Simplified:** `window.trigger('eventname', { arguments: 'here' })`  

<!-- div:right-panel -->
```js
window.trigger('teleport_to', { x: 123, y: 352, z: 385 }); // void
```
```csharp
public class OnTeleportTo : IScript
{
    public OnTeleportTo()
    {
        EventsHandler.Add("teleport_to", new Action<ShPlayer, JToken>(OnEvent));
    }
    public void OnEvent(ShPlayer player, JToken jToken)
    {
        // Beware: does not do any validation work; could easily be a string.
        var vector3 = new Vector3(jToken.Value<float>("x"), jToken.Value<float>("y"), jToken.Value<float>("z"))
        player.SendChatMessage($"Teleporting to {vector3}.."); // Vector3 has an ToString override, hence why this is possible.
        player.svPlayer.SvReset(vector3, player.GetRotation(), player.GetPlaceIndex());
    }
}
```

<!-- div:title-panel -->
## cursorShow

<!-- div:left-panel -->
> Displays cursor for CEF input on client.

**Simplified:** `window.cursorShow()`  

<!-- div:right-panel -->
```js
window.cursorShow(); // void | Shows cursor, that's all.
```

<!-- div:title-panel -->
## cursorHide

<!-- div:left-panel -->
> Hides cursor on client and restores mouse look.

**Simplified:** `window.cursorHide()`  

<!-- div:right-panel -->
```js
window.cursorHide(); // void | Hides cursor, that's all.
```

<!-- div:title-panel -->
## exec `string code`

<!-- div:left-panel -->
> Basically `eval` for C# client side. Executes C# code on the client. View all classes and functions at our API reference [here](https://brokeprotocol.com/api/). But remember, no server data/classes are available on the client.
The global types available for this call are:  
- `ShPlayer` player;  
- `ClPlayer` clPlayer;  
- `ShManager` manager;  
- `ClManager` clManager;  
Try to form your expressions for exec() using a unchanging string input. You might use Linq for example instead of iterating over different array indices explicitly.
This is because any new expression, even one character different will require a complete compilation of your input code, instead of running from a script cache.
![](https://i.imgur.com/MTKOFi2.png)
**First request is new, below that is cached.**

**Simplified:** `window.exec('code')`  

<!-- div:right-panel -->
```js
window.exec('clManager.SendToServer(Channel.Unsequenced, SvPacket.GlobalMessage, "Roslyn Test Message");'); // void
```

<!-- div:title-panel -->
## get `string code`

<!-- div:left-panel -->
> This is similar to exec but actually returns a object result from your input expression.
`get()` is mostly just a helper for `getAsync()` which is an async function used to block until the game returns a result.
Note that the first use of an expression might take 100-300ms but after that should run almost at native C# speed.  
**If the game appears stuck or exhibits extremely poor performance**, check your `get()` and `exec()` functions for errors or input expressions that change each call.
Be mindful that new inputs have to be compiled on their first call.

?> You can see examples of some of these methods being used in the samples present in `YourServerFolder/www/cef/index.html`.

**Simplified:** `window.get('code', (result) => { })`  

<!-- div:right-panel -->
```js
window.get('return player.username', (result) => {
  result // (string) "UserR00T"
});
```

<!-- div:title-panel -->
## getAsync `string code` `(async)`

<!-- div:left-panel -->
> Exactly the same as `get()`, but can be `await`ed using `async`. Highly recommended to use this instead of `get()` to prevent a [callback hell](http://callbackhell.com/).

**Simplified:** `const result = await window.getAsync('code')`  

<!-- div:right-panel -->
```js
const result = await window.getAsync('return player.username');
result // (string) "UserR00T"
```
<!-- panels:end -->