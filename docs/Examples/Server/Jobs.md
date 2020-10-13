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
``selfActions`` -> List of self-action items when you have the job yourself

``employeeActions`` -> List of action items on other players who hold this job

``specialActions`` -> List of action items on other entities in general (note this has an optional Type parameter if you want to filter on only specific types of Entities, ex. "ShPlayer")


## What is the ``Job`` class?
The ``Job`` class refers to the instances of Jobs for each player (NPCs and Humans) on the server. It definces both NPC and Human functionality based on callbacks from the game. Here are some of the functions that can be overridden or used in some way when implementing Jobs (more to come):

```
public virtual float GetSpawnRate();
public void Initialize(ShPlayer player, JobInfo info);
public void MessageAllEmployees(string message);
public virtual void OnDamageEntity(ShEntity damaged);
public virtual void OnDestroyEntity(ShEntity destroyed);
public virtual void OnEmployeeAction(ShPlayer target, string actionID);
public virtual void OnHealEntity(ShEntity damaged);
public virtual void OnJailCriminal(ShPlayer criminal, int fine);
public virtual void OnOptionMenuAction(int targetID, string menuID, string optionID, string actionID);
public virtual void OnRevivePlayer(ShPlayer player);
public virtual void OnSelfAction(string actionID);
public virtual void OnSpecialAction(ShEntity target, string actionID);
public virtual void RemoveJob();
public virtual void ResetJobAI();
public virtual void SetJob();
```

Again, check the GameSource repo for how jobs are managed on a vanilla server as well as how to set up Update loops and send/receive Menu data.

To finalize everything, you must assign the Jobs property in your ``Plugin`` class:
```
public abstract class Plugin
{
    protected Plugin();

    public PluginInfo Info { get; set; }
    public JobInfo[] Jobs { get; set; }
    public CustomData CustomData { get; set; }
}
```

So somewhere in your own Plugin class constructor, assign your custom Job definitions to the Jobs array to have it loaded in-game. Plugins are loaded in alphanumeric order so ``zGameSource`` definitions are usually going to be last. If another plugin has Jobs defined, then those will be loaded instead and the vanilla GameSource jobs ignored. Later on, it should be possible to mix job definitions from different Plugins, but for now, only the first non-null definition is loaded.
