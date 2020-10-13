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


...To be continued...