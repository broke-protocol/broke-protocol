# Database

> This will cover on how to access the database, request user data, add new collections, and more.

## Intro
For 1.0+, we're using a NoSQL database solution. We've settled on using [LiteDB](https://github.com/mbdavid/LiteDB), a _".NET NoSQL Document Store in a single data file"_.

## Collections
These collections can be found in the `Driver` instance. The `Driver` instance can be found in a `SvManager` instance under [`Database`](https://brokeprotocol.com/api/class_broke_protocol_1_1_managers_1_1_sv_manager.html#a5a8eb584e23212f24696520737dbfc4f).

### `Users`
This collection will contain all the user information. All accounts are stored here.  
**Collection Name:** `users`  
**Model:** [`BrokeProtocol.Server.LiteDB.Models.User`](https://brokeprotocol.com/api/class_broke_protocol_1_1_server_1_1_lite_d_b_1_1_models_1_1_user.html) & [`Character`](https://brokeprotocol.com/api/class_broke_protocol_1_1_server_1_1_lite_d_b_1_1_models_1_1_character.html)

### `Bans`
This collection will contain all the ban information keyed by IP address (string format). If a ban is lifted, the record here is removed.  
**Collection Name:** `bans`  
**Model:** [`BrokeProtocol.Server.LiteDB.Models.Ban`](https://brokeprotocol.com/api/class_broke_protocol_1_1_server_1_1_lite_d_b_1_1_models_1_1_ban.html)

### `Settings`
Currently only used for database version.  
**Collection Name:** `setting`  
**Model:** [`BrokeProtocol.Server.LiteDB.Models.Setting`](https://brokeprotocol.com/api/class_broke_protocol_1_1_server_1_1_lite_d_b_1_1_models_1_1_setting.html)

### `Data`
Storing CustomData related to your plugins. Can be used to save data for persistence even after server shutdown. The Hitman Job in GameSource uses this to save Bounties as an example.
**Collection Name:** `data`  
**Model:** [`BrokeProtocol.Server.LiteDB.Models.Data`](https://brokeprotocol.com/api/class_broke_protocol_1_1_server_1_1_lite_d_b_1_1_models_1_1_data.html)

## Driver class
This class contains all the `LiteCollections` and `LiteDatabase` references. This is so we can have a strongly typed list of collections in the code base. This class can be found in the `SvManager` instance, as `Database`. 

## Querying data
For all these examples we assume `svManager` is your `SvManager` instance.  

?> The `User` class and `Character` class have a lot of information. Hence, we cannot show all of it here. See above for links to the reference source and to view each type within those classes.

### Get database version number  
- _Note, this is the actual database file, not the driver version. If you need the driver version, type `Driver.Version`._  
```csharp
var setting = svManager.Database.Settings.FindById("version");
setting.Key // (string) "version"
setting.Value // (string) "1.0" 
```

### Get player by Username  
```csharp
var user = svManager.Database.Users.FindById("NongBenz");
user.ID // (string) "NongBenz"
user.Character.BankBalance // (int) 7
```

### Check if a user is banned
```csharp
var ban = svManager.Database.Bans.FindOne(x => x.Username == "NongBenz");
ban.Reason // (string) "get out >:)"  
```

### Get Collection
```csharp
var collection = svManager.Database.LiteDB.GetCollection<T>("collectionName");
var item = collection.FindOne(x => x.SomeProperty == "somethingCool");
item // (T) T
```

**For more examples & more, visit the [LiteDB](https://github.com/mbdavid/LiteDB/wiki) wiki page.**
