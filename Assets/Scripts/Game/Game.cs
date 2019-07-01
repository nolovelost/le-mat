#define DEBUG_LOGGING
using UnityEngine;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine.Rendering;
#if !UNITY_STANDALONE_LINUX     // Linux doesn't support HDRP due to OpenGL lock
using UnityEngine.Experimental.Rendering.HDPipeline;
#endif
using System;
using System.Globalization;
using UnityEngine.Rendering.PostProcessing;
#if UNITY_EDITOR
using UnityEditor;
#endif


public struct GameTime
{
    /// <summary>Number of ticks per second.</summary>
    public int tickRate
    {
        get { return m_tickRate; }
        set
        {
            m_tickRate = value;
            tickInterval = 1.0f / m_tickRate;
        }
    }

    /// <summary>Length of each world tick at current tickrate, e.g. 0.0166s if ticking at 60fps.</summary>
    public float tickInterval { get; private set; }     // Time between ticks
    public int tick;                    // Current tick
    public float tickDuration;          // Duration of current tick

    public GameTime(int tickRate)
    {
        this.m_tickRate = tickRate;
        this.tickInterval = 1.0f / m_tickRate;
        this.tick = 1;
        this.tickDuration = 0;
    }

    public float TickDurationAsFraction
    {
        get { return tickDuration / tickInterval; }
    }

    public void SetTime(int tick, float tickDuration)
    {
        this.tick = tick;
        this.tickDuration = tickDuration;
    }

    public float DurationSinceTick(int tick)
    {
        return (this.tick - tick) * tickInterval + tickDuration;
    }

    public void AddDuration(float duration)
    {
        tickDuration += duration;
        int deltaTicks = Mathf.FloorToInt(tickDuration * (float)tickRate);
        tick += deltaTicks;
        tickDuration = tickDuration % tickInterval;
    }

    public static float GetDuration(GameTime start, GameTime end)
    {
        if(start.tickRate != end.tickRate)
        {
            GameDebug.LogError("Trying to compare time with different tick rates (" + start.tickRate + " and " + end.tickRate + ")");
            return 0;
        }

        float result = (end.tick - start.tick) * start.tickInterval + end.tickDuration - start.tickDuration;
        return result;
    }

    int m_tickRate;
}

[DefaultExecutionOrder(-1000)]
public class Game : MonoBehaviour
{
    public delegate void UpdateDelegate();

    public static class Input
    {
        [Flags]
        public enum Blocker
        {
            None = 0,
            Console = 1,
            Debug = 2,
        }
        static Blocker blocks;

        public static void SetBlock(Blocker b, bool value)
        {
            if (value)
                blocks |= b;
            else
                blocks &= ~b;
        }

        internal static float GetAxisRaw(string axis)
        {
            return blocks != Blocker.None ? 0.0f : UnityEngine.Input.GetAxisRaw(axis);
        }

        internal static bool GetKey(KeyCode key)
        {
            return blocks != Blocker.None ? false : UnityEngine.Input.GetKey(key);
        }

        internal static bool GetKeyDown(KeyCode key)
        {
            return blocks != Blocker.None ? false : UnityEngine.Input.GetKeyDown(key);
        }

        internal static bool GetMouseButton(int button)
        {
            return blocks != Blocker.None ? false : UnityEngine.Input.GetMouseButton(button);
        }

        internal static bool GetKeyUp(KeyCode key)
        {
            return blocks != Blocker.None ? false : UnityEngine.Input.GetKeyUp(key);
        }
    }

    public interface IGameLoop
    {
        bool Init(string[] args);
        void Shutdown();

        void Update();
        void FixedUpdate();
        void LateUpdate();

        string GetName();
    }

    public static Game game;
    public event UpdateDelegate endUpdateEvent;

    // CVars
    [ConfigVar(Name = "server.tickrate", DefaultValue = "60", Description = "Processing Tickrate", Flags = ConfigVar.Flags.PrimeInfo)]
    public static ConfigVar primeTickRate;

    [ConfigVar(Name = "config.fov", DefaultValue = "60", Description = "Field of view", Flags = ConfigVar.Flags.Save)]
    public static ConfigVar configFov;

    [ConfigVar(Name = "config.mousesensitivity", DefaultValue = "1.5", Description = "Mouse sensitivity", Flags = ConfigVar.Flags.Save)]
    public static ConfigVar configMouseSensitivity;

    [ConfigVar(Name = "config.inverty", DefaultValue = "0", Description = "Invert y mouse axis", Flags = ConfigVar.Flags.Save)]
    public static ConfigVar configInvertY;

    [ConfigVar(Name = "debug.catchloop", DefaultValue = "1", Description = "Catch exceptions in gameloop and pause game", Flags = ConfigVar.Flags.None)]
    public static ConfigVar debugCatchLoop;

    [ConfigVar(Name = "debug.cpuprofile", DefaultValue = "0", Description = "Profile and dump cpu usage")]
    public static ConfigVar debugCpuProfile;

    public static readonly string k_BootConfigFilename = "boot.cfg";
    public static double frameTime;

    public static int GameLoopCount {
        get { return game == null ? 0 : 1; }
    }

    public static T GetGameLoop<T>() where T : class
    {
        if (game == null)
            return null;
        foreach (var gameLoop in game.m_gameLoops)
        {
            T result = gameLoop as T;
            if (result != null)
                return result;
        }
        return null;
    }

    public static System.Diagnostics.Stopwatch Clock
    {
        get { return game.m_Clock; }
    }

    public void RequestGameLoop(System.Type type, string[] args)
    {
        GameDebug.Assert(typeof(IGameLoop).IsAssignableFrom(type));

        m_RequestedGameLoopTypes.Add(type);
        m_RequestedGameLoopArguments.Add(args);
        GameDebug.Log("Game loop " + type + " requested");
    }

    // Pick argument for argument(!). Given list of args return null if option is
    // not found. Return argument following option if found or empty string if none given.
    // Options are expected to be prefixed with + or -
    public static string ArgumentForOption(List<string> args, string option)
    {
        var idx = args.IndexOf(option);
        if (idx < 0)
            return null;
        if (idx < args.Count - 1)
            return args[idx + 1];
        return "";
    }

    public void Awake()
    {
        GameDebug.Log("-- Game Awakeing --");

        GameDebug.Assert(game == null);
        //DontDestroyOnLoad(gameObject);
        game = this;

        m_StopwatchFrequency = System.Diagnostics.Stopwatch.Frequency;
        m_Clock = new System.Diagnostics.Stopwatch();
        m_Clock.Start();

        var commandLineArgs = new List<string>(System.Environment.GetCommandLineArgs());

        var consoleRestoreFocus = commandLineArgs.Contains("-consolerestorefocus");

        var consoleUI = Instantiate(Resources.Load<ConsoleGUI>("Prefabs/ConsoleGUI"));
        DontDestroyOnLoad(consoleUI);
        Console.Init(consoleUI);

        m_DebugOverlay = Instantiate(Resources.Load<DebugOverlay>("Prefabs/DebugOverlay"));
        DontDestroyOnLoad(m_DebugOverlay);
        m_DebugOverlay.Init();

        // If -logfile was passed, we try to put our own logs next to the engine's logfile
        var engineLogFileLocation = ".";
        var logfileArgIdx = commandLineArgs.IndexOf("-logfile");
        if(logfileArgIdx >= 0 && commandLineArgs.Count >= logfileArgIdx)
        {
            engineLogFileLocation = System.IO.Path.GetDirectoryName(commandLineArgs[logfileArgIdx + 1]);
        }

        var logName = m_isHeadless ? "game_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") : "game";
        GameDebug.Init(engineLogFileLocation, logName);

        ConfigVar.Init();

#if UNITY_EDITOR
        GameDebug.Log("Build type: editor");
#elif DEVELOPMENT_BUILD
        GameDebug.Log("Build type: development");
#else
        GameDebug.Log("Build type: release");
#endif
        //GameDebug.Log("BuildID: " + buildId);
        GameDebug.Log("Cwd: " + System.IO.Directory.GetCurrentDirectory());


        // Game loops
        Console.AddCommand("zero", CmdZero, "Start preview mode");

        Console.AddCommand("gameloops", CmdGameLoops, "List all current game loops.");
        Console.AddCommand("clear", CmdClear, "Shutdown all initilized IGameLoop provided as argument.");
        Console.AddCommand("reset", CmdResetLoop, "Resets all initilized IGameLoop provided as argument.");

        // Utility commands
        Console.AddCommand("quit", CmdQuit, "Quits");
        Console.AddCommand("screenshot", CmdScreenshot, "Capture screenshot. Optional argument is destination folder or filename.");
        Console.AddCommand("crashme", (string[] args) => { GameDebug.Assert(false); }, "Crashes the game next frame ");

#if UNITY_STANDALONE_WIN
        Console.AddCommand("windowpos", CmdWindowPosition, "Position of window. e.g. windowpos 100,100");
#endif

        Console.SetOpen(true);
        Console.ProcessCommandLineArguments(commandLineArgs.ToArray());

        if (k_BootConfigFilename != null)
            Console.EnqueueCommandNoHistory("exec -s " + k_BootConfigFilename);

        GameDebug.Log("-- Game Awake --");
    }

    void OnDestroy()
    {
        GameDebug.Shutdown();
        Console.Shutdown();
        if (m_DebugOverlay != null)
            m_DebugOverlay.Shutdown();
    }

    public void Update()
    {
#if UNITY_EDITOR
        // Ugly hack to force focus to game view when using scriptable renderloops.
        if (Time.frameCount < 4)
        {
            try
            {
                var gameViewType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
                var gameView = (EditorWindow)Resources.FindObjectsOfTypeAll(gameViewType)[0];
                gameView.Focus();
            }
            catch (System.Exception) { /* too bad */ }
        }
#endif

        frameTime = (double)m_Clock.ElapsedTicks / m_StopwatchFrequency;

        // Switch game loop if needed
        if (m_RequestedGameLoopTypes.Count > 0)
        {
            bool initSucceeded = false;
            for(int i=0;i<m_RequestedGameLoopTypes.Count;i++)
            {
                try
                {
                    IGameLoop gameLoop = (IGameLoop)System.Activator.CreateInstance(m_RequestedGameLoopTypes[i]);
                    initSucceeded = gameLoop.Init(m_RequestedGameLoopArguments[i]);
                    if (!initSucceeded)
                        break;

                    m_gameLoops.Add(gameLoop);
                }
                catch (System.Exception e)
                {
                    GameDebug.Log(string.Format("Game loop initialization threw exception : ({0})\n{1}", e.Message, e.StackTrace));
                }
            }


            if (!initSucceeded)
            {
                ShutdownGameLoops();

                GameDebug.Log("Game loop initialization failed ... reverting to boot loop");
            }

            m_RequestedGameLoopTypes.Clear();
            m_RequestedGameLoopArguments.Clear();
        }

        try
        {
            if (!m_ErrorState)
            {
                foreach (var gameLoop in m_gameLoops)
                {
                    gameLoop.Update();
                }
            }
        }
        catch (System.Exception e)
        {
            HandleGameloopException(e);
            throw;
        }

        Console.ConsoleUpdate();

        WindowFocusUpdate();

        UpdateCPUStats();

        endUpdateEvent?.Invoke();
    }

    bool m_ErrorState;

    public void FixedUpdate()
    {
        foreach (var gameLoop in m_gameLoops)
        {
            gameLoop.FixedUpdate();
        }

    }

    public void LateUpdate()
    {
        try
        {
            if (!m_ErrorState)
            {
                foreach (var gameLoop in m_gameLoops)
                {
                    gameLoop.LateUpdate();
                }
                Console.ConsoleLateUpdate();
            }
        }
        catch (System.Exception e)
        {
            HandleGameloopException(e);
            throw;
        }

        if (m_DebugOverlay != null)
            m_DebugOverlay.TickLateUpdate();
    }

    void OnApplicationQuit()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        GameDebug.Log("Farewell, cruel world...");
        System.Diagnostics.Process.GetCurrentProcess().Kill();
#endif
        ShutdownGameLoops();
    }

    float m_NextCpuProfileTime = 0;
    double m_LastCpuUsage = 0;
    double m_LastCpuUsageUser = 0;
    void UpdateCPUStats()
    {
        if(debugCpuProfile.IntValue > 0)
        {
            if(Time.time > m_NextCpuProfileTime)
            {
                const float interval = 5.0f;
                m_NextCpuProfileTime = Time.time + interval;
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var user = process.UserProcessorTime.TotalMilliseconds;
                var total = process.TotalProcessorTime.TotalMilliseconds;
                float userUsagePct = (float)(user - m_LastCpuUsageUser) / 10.0f / interval;
                float totalUsagePct = (float)(total- m_LastCpuUsage) / 10.0f / interval;
                m_LastCpuUsage = total;
                m_LastCpuUsageUser = user;
                GameDebug.Log(string.Format("CPU Usage {0}% (user: {1}%)", totalUsagePct, userUsagePct));
            }
        }
    }

    void HandleGameloopException(System.Exception e)
    {
        if (debugCatchLoop.IntValue > 0)
        {
            GameDebug.Log("EXCEPTION " + e.Message + "\n" + e.StackTrace);
            Console.SetOpen(true);
            m_ErrorState = true;
        }
    }

    string FindNewFilename(string pattern)
    {
        for(var i = 0; i < 10000; i++)
        {
            var f = string.Format(pattern, i);
            if (System.IO.File.Exists(string.Format(pattern, i)))
                continue;
            return f;
        }
        return null;
    }

    void ShutdownGameLoops()
    {
        foreach (var gameLoop in m_gameLoops)
            gameLoop.Shutdown();
        m_gameLoops.Clear();
    }

    void CmdZero(string[] args)
    {
        RequestGameLoop(typeof(ZeroGameLoop), args);
        Console.s_PendingCommandsWaitForFrames = 1;
    }

    void CmdGameLoops(string[] args)
    {
        if (m_gameLoops.Count > 0)
        {
            int count = 0;
            foreach (var gameLoop in m_gameLoops)
            {
                Console.Write("m_gameLoops[" + (count++) + "] = " + gameLoop.GetName());
            }
        }
        else
        {
            Console.Write("No GameLoops initialized");
        }
    }

    void CmdClear(string[] args)
    {
        if (args.Length > 0)
        {
            List<IGameLoop> requestRemoveLoop = new List<IGameLoop>();
            int loopCount = 0;
            for (int i = 0; i < m_gameLoops.Count; i++)
            {
                for (int j = 0; j < args.Length; j++)
                {
                    if (m_gameLoops[i].GetType().ToString() == args[j] ||
                        m_gameLoops[i].GetName() == args[j])
                    {
                        ++loopCount;
                        var gameLoop = m_gameLoops[i];
                        gameLoop.Shutdown();
                        requestRemoveLoop.Add(gameLoop);
                    }
                }
            }
            foreach (var removeLoop in requestRemoveLoop)
            {
                m_gameLoops.Remove(removeLoop);
            }
            if (loopCount == 0)
                Console.Write("IGameLoop not found.");
        }
        else
        {
            Console.Write("Provide an IGameLoop type or name as an argument.");
        }
    }

    void CmdResetLoop(string[] args)
    {
        if (args.Length > 0)
        {
            List<IGameLoop> requestResetLoop = new List<IGameLoop>();
            int loopCount = 0;
            for (int i = 0; i < m_gameLoops.Count; i++)
            {
                for (int j = 0; j < args.Length; j++)
                {
                    if (m_gameLoops[i].GetType().ToString() == args[j] ||
                        m_gameLoops[i].GetName() == args[j])
                    {
                        ++loopCount;
                        var gameLoop = m_gameLoops[i];
                        RequestGameLoop(gameLoop.GetType(), args);
                        Console.s_PendingCommandsWaitForFrames = 1;
                        gameLoop.Shutdown();
                        requestResetLoop.Add(gameLoop);
                    }
                }
            }
            foreach (var resetLoop in requestResetLoop)
            {
                m_gameLoops.Remove(resetLoop);
            }
            if (loopCount == 0)
                Console.Write("IGameLoop not found.");
        }
        else
        {
            Console.Write("Provide an IGameLoop type or name as an argument.");
        }
    }

    void CmdQuit(string[] args)
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void CmdScreenshot(string[] arguments)
    {
        string filename = null;
        var root = System.IO.Path.GetFullPath(".");
        if (arguments.Length == 0)
            filename = FindNewFilename(root+"/screenshot{0}.png");
        else if (arguments.Length == 1)
        {
            var a = arguments[0];
            if (System.IO.Directory.Exists(a))
                filename = FindNewFilename(a + "/screenshot{0}.png");
            else if (!System.IO.File.Exists(a))
                filename = a;
            else
            {
                Console.Write("File " + a + " already exists");
                return;
            }
        }
        if (filename != null)
        {
            GameDebug.Log("Saving screenshot to " + filename);
            Console.SetOpen(false);
            ScreenCapture.CaptureScreenshot(filename);
        }
    }

#if UNITY_STANDALONE_WIN
    void CmdWindowPosition(string[] arguments)
    {
        if (arguments.Length == 1)
        {
            string[] cords = arguments[0].Split(',');
            if (cords.Length == 2)
            {
                int x, y;
                var xParsed = int.TryParse(cords[0], out x);
                var yParsed = int.TryParse(cords[1], out y);
                if (xParsed && yParsed)
                {
                    WindowsUtil.SetWindowPosition(x, y);
                    return;
                }
            }
        }
        Console.Write("Usage: windowpos <x,y>");
    }

#endif

    public static void RequestMousePointerLock()
    {
        s_bMouseLockFrameNo = Time.frameCount + 1;
    }

    public static void SetMousePointerLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
        s_bMouseLockFrameNo = Time.frameCount; // prevent default handling in WindowFocusUpdate overriding requests
    }

    public static bool GetMousePointerLock()
    {
        return Cursor.lockState == CursorLockMode.Locked;
    }

    void WindowFocusUpdate()
    {
        bool lockWhenClicked = !Console.IsOpen();

        if(s_bMouseLockFrameNo == Time.frameCount)
        {
            SetMousePointerLock(true);
            return;
        }

        if (lockWhenClicked)
        {
            // Default behaviour when no menus or anything. Catch mouse on click, release on escape.
            if (UnityEngine.Input.GetMouseButtonUp(0) && !GetMousePointerLock())
                SetMousePointerLock(true);

            if (UnityEngine.Input.GetKeyUp(KeyCode.Escape) && GetMousePointerLock())
                SetMousePointerLock(false);
        }
        else
        {
            // When menu or console open, release lock
            if (GetMousePointerLock())
            {
                SetMousePointerLock(false);
            }
        }
    }

    List<Type> m_RequestedGameLoopTypes = new List<System.Type>();
    private List<string[]> m_RequestedGameLoopArguments = new List<string[]>();

    List<IGameLoop> m_gameLoops = new List<IGameLoop>();
    DebugOverlay m_DebugOverlay;

    bool m_isHeadless;
    long m_StopwatchFrequency;
    System.Diagnostics.Stopwatch m_Clock;

    static int s_bMouseLockFrameNo;
}
