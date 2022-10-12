# AI States

> This example will cover the basics of the ``State`` class in setting up AI behaviors. Each state is basically the mode in which the AI is operating. Only a single State is active at any time but a State can be a heirarchy of classes. In this sense, BP uses a Heirarchical Finite State Machine (HFSM) for clear state Entry/Exit events. But it also uses Behavior Tree (BT) decision tree in ResetAI to clearly determine the current state. A continuous loop will call UpdateState() on the AI every 0.1s so you can evaluate and execute the currect AI State or switch to another State.

## What is the ``State`` class?
The ``State`` is the class that defines what happens when a State is entered, updated, and exited. This includes:
```
public virtual byte StateMoveMode;
public virtual bool IsBusy;
public virtual bool EnterTest();
public virtual void EnterState();
public virtual bool UpdateState();
public virtual void ExitState(State nextState);
```

Most are self-explanitory though you can check the LifeSource or WarSource to see how all the current AI states are defined.

To initialize your custom states, you must assign them to either the StatesAdditive or StatesOverride in your ``Plugin`` class:

First I like to Instantiate the states as public static variables, so player.svPlayer.SetState(int stateIndex) can access the State from anywhere"
```cs
public static State Rob = new RobState();
public static State PullOver = new PullOverState();
```

Then somewhere in your own Plugin class constructor, assign your custom States definitions as a List to either StatesAdditive or StatesOverride. StatesAdditive merely adds your state to the existing list of states from previously loaded Plugins. StatesOverride will remove all previously existing states and start fresh with your current list of states if you want to redefine everything from scratch.

```cs
StatesAdditive = new List<State>
{
    Rob,
    PullOver,
};
```

Now you are ready to Call player.svPlayer.SetState(Rob.index) from anywhere. The most important areas for AI are the Player ResetAI event and the ResetJobAI virtual method in the Job class. These are evaluated every time the AI 'Resets' and you need to evaluate the AI current state and call SetState. See GameSource, LifeSource, and WarSource for implementations of these.
