using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UI;


public class ZeroGameLoop : Game.IGameLoop
{
    enum ZeroGameState
    {
        Loading,
        Active
    }
    StateMachine<ZeroGameState> m_StateMachine;

    GameTime gameTime = new GameTime(60);

    // Kinematic Player Character
    private GameObject kinematicContainer;
    GameObject exampleChar;

    GameWorld gameWorld;

    public bool Init(string[] args)
    {
        gameWorld = new GameWorld("[WORLD]ZeroGameWorld");

        m_StateMachine = new StateMachine<ZeroGameState>();
        m_StateMachine.Add(ZeroGameState.Loading, null, UpdateLoadingState, null);
        m_StateMachine.Add(ZeroGameState.Active, EnterActiveState, UpdateStateActive, LeaveActiveState);

        Console.SetOpen(false);

        if (args.Length > 0)
        {
            // ...Load level here...

            m_StateMachine.SwitchTo(ZeroGameState.Loading);
        }
        else
        {
            m_StateMachine.SwitchTo(ZeroGameState.Active);
        }

        GameDebug.Log("ZeroGame initialized");
        return true;
    }

    public void Shutdown()
    {
        GameDebug.Log("ZeroGameState shutdown");
        Console.RemoveCommandsWithTag(this.GetHashCode());

        Object.Destroy(exampleChar);
        exampleChar = null;
        Object.Destroy(kinematicContainer);
        kinematicContainer = null;

        gameWorld.Shutdown();
        m_StateMachine.Shutdown();

        // ...Unload Level Here...
    }

    void UpdateLoadingState()
    {
        //if (Game.game.levelManager.IsCurrentLevelLoaded())
        //    m_StateMachine.SwitchTo(PreviewState.Active);
        m_StateMachine.SwitchTo(ZeroGameState.Active);
    }

    public void Update()
    {
        m_StateMachine.Update();
    }

    void EnterActiveState()
    {
        Game.SetMousePointerLock(true);

        // Instantiate Kinematic Character Controller
        kinematicContainer = new GameObject("~~~ KINEMATIC CONTROLLER ~~~");
        exampleChar = Object.Instantiate<GameObject>(Resources.Load<GameObject>("KinematicCharacter/ExampleCharacter"));
        exampleChar.AddComponent<GameObjectEntity>();
        exampleChar.transform.SetParent(kinematicContainer.transform, true);
        // #TODO: Remove this hack and utilise spawn points system instead
        exampleChar.transform.position = new Vector3(0.0f, 2.0f, 0.0f);
    }

    void LeaveActiveState()
    {
    }

    void UpdateStateActive()
    {
        // Sample input
        bool userInputEnabled = Game.GetMousePointerLock();

        // #NOTE: This overrides the member variable tickRate. But why?
        if (gameTime.tickRate != Game.primeTickRate.IntValue)
            gameTime.tickRate = Game.primeTickRate.IntValue;

        while (Game.frameTime > gameWorld.nextTickTime)
        {
            gameTime.tick++;
            gameTime.tickDuration = gameTime.tickInterval;

            ZeroGameTickUpdate();
            gameWorld.nextTickTime += gameWorld.worldTime.tickInterval;
        }
    }


    public void FixedUpdate()
    {
    }

    public void ZeroGameTickUpdate()
    {
    }

    public void LateUpdate()
    {
        // TODO (petera) Should the state machine actually have a lateupdate so we don't have to do this always?
        if (m_StateMachine.CurrentState() == ZeroGameState.Active)
        {
        }
    }

    public string GetName()
    {
        return "zero";
    }
}
