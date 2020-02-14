# 05: Group Manager

> The group manager is a type used in the API for permissions and groups.

## groups.json
First, read the `groups.json` file. This will explain each type within the JSON context.

If you ever need a template, it's here!
```js
{
  "Type": "",
  "Name": "",
  "Permissions": [
  ],
  "CustomData": {
    "Data": {
    }
  },
  "Members": [
  ],
  "Inherits": [
  ]
},
```

### Type
This field is used to set the group member type.
The following types are allowed (Defined in "BrokeProtocol.API.GroupTypes"
- UserSteamID
    - The user steam64id.
      - Selector: `player.steamID`
      - Example: `76561198088598550`
- UserIP
    - The user IPv4.
      - Selector: `player.svPlayer.connection.IP`
      - Example: `127.0.0.1`
- JobIndex
    - The user JobID.
      - Selector: `player.job.jobIndex`
      - Example: `3`
- JobName
    - The user job name.
      - Selector: `player.job.info.jobName`
      - Example: `Police`
- JobGroupIndex
    - The user job group index. `0 = Citizen, 1 = Criminal, 2 = LawEnforcement`
      - Selector: `player.job.info.groupIndex`
      - Example: `2`
- ScriptableTrigger
    - The identifier of the scriptable trigger the user is currently in. (if any)
      - Selector: `"player.svPlayer.currentTrigger.identifier`
      - Example: `SomeStringIdentifier`

```js
"Type": "UserSteamID",
"Members": [
  "76561198088598550"
]

// Or
"Type": "UserIP",
"Members": [
  "127.0.0.1"
]

// Or
"Type": "JobName",
"Members": [
  "Police"
]

// etc. etc.
```

### Name
Name can be used to differentiate the group from other groups. For example `Admins` and `Mods`. You name it.

### Permissions
This is the list with permissions the group has. All these permissions are to be provided in `string` format. This is permission node based, which will allow you to type `.HasPermission("hello.world.member");`. Then within the `Permissions` array, you can add the following entry:
```js
"Permissions": [
  "hello.world.member"
],
```
Which when running the `HasPermission` method, will return `true`. 

You can also use wildcards (`*`), which will allow anything within that node. So the following would also return `true`:
```js
"Permissions": [
  "hello.world.*"
],
```
And so will
```js
"Permissions": [
  "hello.*"
],
```
And
```js
"Permissions": [
  "*"
],
```
Cool, cool.

This is also the same for members. Because the `Members` array doesn't use nodes, you can only type the current group type format or `*`. Both are to be provided in `string` format.

### CustomData.Data
CustomData can be set per group and can be used by resources. This implements the `CustomData` class and `ICustomData` interface. This uses a `Dictionary<string, object>` low level, so technically any custom entry is possible.

### Members
Here you'll define everyone who has permission to this group. This is to be provided always as a `string`.

```js
"Members": [
  "*"
],
```
Will allow any member, and
```js
"Type": "UserSteamID",
"Members": [
  "76561198088598550"
],
```
will only allow the member with the steam id `76561198088598550`.

### Inherits
This field is used to inherit the permissions from the groups in it. This uses recursion, so you can have nested inherits.
```js
"Type": "UserSteamID",
"Name": "Owner",
"Inherits": [
  "Admin"
],
"Permissions": [
  "bp.ban"
]

// ...
"Name": "Admin",
"Inherits": [
  "Mod"
],
"Permissions": [
  "bp.kick"
]

// ...
"Name": "Mod",
"Inherits": [
  "Default"
],
"Permissions": [
  "bp.teleport",
  "bp.restrain"
]

// ...
"Name": "Default",
"Inherits": [
],
"Permissions": [
  "bp.voteStart"
]
```
Now `Owner` has permission to `bp.ban`, but also to:
```
bp.kick
bp.teleport
bp.restrain
```
and finally, `bp.voteStart`.


## Commands & Group Manager

Commands using the `CommandHandler` will use the `GroupNamespace` defined in server example 01. This will then be the prefix of the command name, so that when you create a command with the name `World` and a group namespace with `Hello`, it'll be `Hello.World`. (Note, permission checks are **case-insensitive**.)

## Exposed members

The following members are exposed from the `Groups.cs` class:
```csharp
// Properties
GroupType Type
string Name
List<string> Permissions
ICustomData CustomData
List<string> Members

// Methods
bool IsMember(SvPlayer player)
bool HasOverrideMembers()
bool HasOverridePermission(SvPlayer player)
bool HasPermission(SvPlayer player, string permission)
bool AddMember(SvPlayer player)
bool RemoveMember(SvPlayer player)
void ClearMembers()
void ClearPermissions()
```

`GroupManager.cs`
```csharp
// Properties
static List<Group> Groups

// Methods
static void ReadGroups()
static void WriteGroups()
static List<Group> GetOverrideGroups()
```

The word `Override` is in this case implying the group has a wildcard (`*`) for members or permissions, depending on the method.