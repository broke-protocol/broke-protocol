# AI States

> This example will cover the basics of the ``State`` class in setting up AI behaviors. Each State is basically the mode in which the AI is operating like Attack, Rob, or Wander. Only a single State is active at any time but a State can be a heirarchy of classes. In this sense, BP uses a Heirarchical Finite State Machine (HFSM) for clear state Entry/Exit events. But it also uses a Behavior Tree (BT) decision tree in ResetAI/ResetJobAI to evaluate which State to enter. A continuous loop will call UpdateState() on the AI every 0.1s so you can execute the current AI State or transition to another State.

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

Most are self-explanitory though you can check the LifeSource or WarSource to see how all the current AI States are defined.

To initialize your custom State class, you must assign them to either the StatesAdditive or StatesOverride in your ``Plugin`` class:

First, I like to Instantiate the States as public static variables, so player.svPlayer.SetState(int stateIndex) can access the State from anywhere
```cs
public static State Rob = new RobState();
public static State PullOver = new PullOverState();
```

Then somewhere in your own Plugin class constructor, assign your custom State definitions as a List to either StatesAdditive or StatesOverride. StatesAdditive merely adds your state to the existing list of states from previously loaded Plugins. StatesOverride will remove all previously existing States and replace them with your current list of States if you want to redefine existing AI behavior.
```cs
StatesAdditive = new List<State>
{
    Rob,
    PullOver,
};
```

Now you are ready to Call player.svPlayer.SetState(Rob.index) from anywhere. The most important areas for AI are the Player ResetAI event and the ResetJobAI virtual method in the Job class. These are evaluated every time the AI 'Resets' and you need to Re-evaluate your AI decision tree and call SetState on the most suitable State. See GameSource, LifeSource, and WarSource for implementations of these.

> Important note: If at any time in your UpdateState() implementation, you call ResetAI() or SetState(), then return `false`. And check the return state of base.UpdateState() so you can guard against running subclass code of the State after the State has already changed.
