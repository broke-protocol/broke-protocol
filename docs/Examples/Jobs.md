# Jobs

> This example will cover the basics of the ``JobInfo`` and ``Job`` classes in setting up custom Jobs. These classes are used for storing job metadata and functionality respectively.

## What is the ``JobInfo`` class?
The ``JobInfo`` is the metadata class that defines a job. This includes:
```
public readonly string jobName;
public readonly string jobDescription;
public readonly GroupIndex groupIndex;
public readonly ColorStruct jobColor;
public readonly List<TypeLabelID> specialActions;
public readonly List<LabelID> employeeActions;
public readonly List<LabelID> selfActions;
public byte jobIndex;
public Type jobType;
public int maxCount;
public int attackLevel;
public float spawnRate;
public int poolSize;
public Transports[] transports;
public Upgrades[] upgrades;
```

Most are self-explanitory though you can check the GameSource to see how all of the default Jobs are defined.
One point that does need a little explaination are the Lists of Actions. These are the custom Action Menu items that pop up on different entities.

- ``selfActions`` -> List of self-action items when you have the job yourself
- ``employeeActions`` -> List of action items on other players who hold this job
- ``specialActions`` -> List of action items on other entities in general (note this has an optional Type parameter if you want to filter on only specific types of Entities, ex. "ShPlayer")


## What is the ``Job`` class?
The ``Job`` class refers to the instances of Jobs for each player (NPCs and Humans) on the server. It definces both NPC and Human functionality based on callbacks from the game. Here are some of the functions that can be overridden or used in some way when implementing Jobs (more to come):

```
public virtual float GetSpawnRate() => 0f;
public virtual void ResetJob() { }
public virtual void SetJob() => info.members.TryAdd(player);
public virtual void RemoveJob() => info.members.Remove(player);
public virtual void ResetJobAI() { }
public virtual bool IsValidTarget(ShPlayer chaser) => true;
public virtual ShUsable GetBestJobEquipable() => null;
public virtual void OnSpawn() { }
public virtual void OnDie() { }
public virtual void OnRevivePlayer(ShPlayer player) { }
public virtual void OnHealEntity(ShEntity entity) { }
public virtual void OnDamageEntity(ShEntity damaged) { }
public virtual void OnDestroyEntity(ShEntity destroyed) { }
public virtual void OnOptionMenuAction(int targetID, string id, string optionID, string actionID) { }
```

Again, check the GameSource repo for how jobs are managed on a vanilla server as well as how to set up Update loops and send/receive Menu data.

To finalize everything, you must assign it to either the JobsAdditive or JobsOverride property in your ``Plugin`` class:
```
public abstract class Plugin
{
    protected Plugin();
    public List<JobInfo> JobsOverride { get; set; }
    public List<JobInfo> JobsAdditive { get; set; }
}
```

So somewhere in your own Plugin class constructor, assign your custom Job definitions to the JobsAdditive or JobsOverride List to have it loaded in-game. Plugins are loaded in alphanumeric order so ``zGameSource`` definitions are usually going to be last. If another plugin has Jobs defined, then those will be loaded instead and the vanilla GameSource jobs ignored. Later on, it should be possible to mix job definitions from different Plugins, but for now, only the first non-null definition is loaded.

## Adding a Job Example
Here we will show how to add an additional Job to Broke Protocol. Everything from job parameters, logic, Boss modding, and more will be covered. The example will be a Mechanic job so players can get rewards for vehicle repairs, but nearly anything could be created.

The TargetEntity class does a lot of work with marking targets on the map and continually checking if they're valid so we'll use that as the parent class. Define the Mechanic Job in your plugin as its own class like below:

```cs
public class Mechanic : TargetEntityJob
{
    // How we find a random Transport/Vehicle in-game: Select random from Entities until a ShTransport type is found
    protected override GetEntityCallback GetTargetHandler() => () => EntityCollections.Entities.ElementAt(Random.Range(0, EntityCollections.Entities.Count)) as ShTransport;

    // Exit conditions and Retargeting checks in a loop
    public override void Loop()
    {
        if (player.IsDead) return;

        if (player.isHuman && !ValidTarget(target)) SetTarget();
    }

    // What happens when a valid target is found (a damaged vehicle)
    protected override void FoundTarget()
    {
        base.FoundTarget();
        player.svPlayer.SendGameMessage(target.name + " vehicle is damaged! Check map");
    }

    // Conditions for a valid target (is Transport and damaged-> health < maxStat)
    protected override bool ValidTarget(ShEntity target) =>
        base.ValidTarget(target) && target is ShTransport transport && transport.health < transport.maxStat;

    // What do we do when player heals an entity (reward if entity is target)
    public override void OnHealEntity(ShEntity entity)
    {
        if(entity == target) player.svPlayer.Reward(2, 150);
    }
}
```

Also in your Plugin class you want to add your new job to the List<JobInfo> JobsAdditive or JobsOverride Lists as seen in https://github.com/broke-protocol/broke-protocol/blob/master/BP-GameSource/Core.cs

```cs
JobsAdditive.Add(new JobInfo(
                typeof(Citizen), "Mechanic",
                "Repair damaged vehicles for cash rewards",
                CharacterType.Humanoid, 0, new ColorStruct(0.9f, 0.9f, 0.9f),
                new Upgrades[] {
                    new Upgrades(10,
                        new InventoryStruct[] {
                            new InventoryStruct("Toolkit", 5),
                            new InventoryStruct("HatBoonieDark", 1)})
                }));
```

Adjust any job items or any other parameters to your liking (See the JobInfo class for more parameter descriptions or look at other jobs to see how they define parameters). Note that all jobs must be defined & assigned in the same plugin. There is no additive mixing of jobs from different plugins (yet).

Next, we need a Boss to actually give the job in-game. There is no Boss for this job defined yet in the game so a new one must be modded in. Follow the guide for modding here: https://brokeprotocol.com/modding-guide/.

You should duplicate another Boss in Unity, find a skin for it online (Synty character skins) or create your own and assign your created Material/Texture onto the model. Then set the Spawn Job Index field to the index of your new job (the index/order within the Jobs array in your Plugin).

![Job Modding](https://brokeprotocol.com/wp-content/uploads/JobModding.png)

After you export and add your BPA file to the games AssetBundles directory, you should then be able to place it in your map. And you're all set! That's all it takes to add a new Job to Broke Protocol.
