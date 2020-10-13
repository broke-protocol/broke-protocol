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


### `Settings`
Currently only used for database version.  
**Collection Name:** `setting`  
**Model:** [`BrokeProtocol.Server.LiteDB.Models.Setting`](https://brokeprotocol.com/api/class_broke_protocol_1_1_server_1_1_lite_d_b_1_1_models_1_1_setting.html)

## Driver class
This class contains all the `LiteCollections` and `LiteDatabase` references. This is so we can have a strongly typed list of collections in the code base. This class can be found in the `SvManager` instance, as `Database`. 

## Querying data
For all these examples we assume `svManager` is your `SvManager` instance.  

?> The `User` class and `Character` class have a lot of information. Hence, we cannot show all of it here. See above for links to the reference source and to view each type within those classes.

### Get database version number  
- _Note, this is the actual database file, not the driver version. If you need the driver version, type `Driver.Version`._  
```csharp
var setting = svManager.Database.Settings.FindOne(x => x.Key == "version");
setting.Key // (string) "version"
setting.Value // (string) "1.0" 
```

### Get player by SteamID  
- _Note, after doing various testing we noticed that `FindById` did not give the results we expected. Hence, we're using `FindOne` here._  
```csharp
var user = svManager.Database.Users.FindOne(x => x.ID == "76561198088598550");
user.Character.Username // (string) "UserR00T"
user.Character.BankBalance // (int) 7
```

### Check if player is banned by SteamID
```csharp
var user = svManager.Database.Users.FindOne(x => x.ID == "76561198088598550");
if (user.BanInfo.IsBanned)
{
  // The user is banned, do something cool
  user.BanInfo.Reason // (string) "get out >:)"  
}
```
You could also write it as such, but less performant: [(see here why)](https://github.com/mbdavid/LiteDB/wiki/Indexes#changes-in-v4)
```csharp
var user = svManager.Database.Users.FindOne(x => x.ID == "76561198088598550" && x.BanInfo.IsBanned);
if (user != null)
{
  // The user is banned, do something cool
  user.BanInfo.Reason // (string) "get out >:)"  
}
```

### Get Collection
```csharp
var collection = svManager.Database.LiteDB.GetCollection<T>("collectionName");
var item = collection.FindOne(x => x.SomeProperty == "somethingCool");
item // (T) T
```

**For more examples & more, visit the [LiteDB](https://github.com/mbdavid/LiteDB/wiki) wiki page.**
