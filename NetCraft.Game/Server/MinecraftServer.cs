using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NetCraft;
using NetCraft.Commands;
using NetCraft.Game.Commands;
using NetCraft.Game.World.Clock;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.Timers;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.Server;

//MinecraftServer server main loop, maps to vanilla net.minecraft.server.MinecraftServer
//Holds the running state and Shutdown signal, providing the Tick and Run main loop skeleton
//Phase 11.35 wired the loop shell; phase 11.46 adds tick interval control to avoid 100% CPU, aligned with vanilla 20 TPS
//The DedicatedServer subclass wires in PersistentServerLevel real save scheduling
public abstract class MinecraftServer
{
    //TargetTps target ticks per second, aligned with vanilla 20 TPS
    public const int TargetTps = 20;
    //TargetTickMillis per-tick target duration, 50ms
    public const int TargetTickMillis = 1000 / TargetTps;

    //OverloadedThresholdMillis overload is only declared when lagging exceeds this, aligned with vanilla OVERLOADED_THRESHOLD_NANOS i.e. 1 second
    public const long OverloadedThresholdMillis = 1000;
    //OverloadedWarningIntervalMillis minimum interval between overload warnings, aligned with vanilla OVERLOADED_WARNING_INTERVAL_NANOS i.e. 10 seconds
    public const long OverloadedWarningIntervalMillis = 10000;

    private readonly Thread _serverThread;
    private volatile bool _running;
    private long _tickCount;
    private readonly CancellationTokenSource _shutdownCts = new();
    //_worldGate world state gate; each main loop tick and shutdown flush are mutually exclusive
    //Vanilla stopServer is submitted to the server thread and naturally serializes with tick
    //Here Stop may come from Ctrl+C or the GUI thread; without serialization it would snapshot mid-piston-move
    //In such a snapshot the block is a moving piston but the block entity is not yet registered, and that cell reads back stuck forever
    private readonly object _worldGate = new();
    //_consoleCommands commands posted from the console are only queued and taken by the main loop to execute
    //maps to vanilla DedicatedServer's serverCommandQueue and handleConsoleInputs
    //If the command line thread ran the command directly the world state would interleave with the main loop and the command could not get a consistent snapshot while the loop holds the gate
    //Done is for callers that need a synced reply (RCON); the main loop releases the blocker after executing, and plain posts pass null
    private readonly ConcurrentQueue<(CommandSourceStack Source, string Command, TaskCompletionSource<object?>? Done)> _consoleCommands = new();
    //SleepBudgetMillis per-tick sleep budget; no sleep when below 1, to avoid 100% CPU
    private int _sleepBudgetMillis = TargetTickMillis;
    //_tickClock main loop timeline, monotonic, used for the nextTickTime lag calculation
    private readonly Stopwatch _tickClock = Stopwatch.StartNew();
    //_nextTickTimeMillis the target start time of the next tick; the lag is derived from it, maps to vanilla nextTickTimeNanos
    private long _nextTickTimeMillis;
    //_lastOverloadWarningMillis the timeline position of the last overload warning, used to throttle warnings
    private long _lastOverloadWarningMillis;
    //StartWatch server start timing begins at construction, maps to the vanilla server start moment for the Done log
    private readonly Stopwatch _startWatch = Stopwatch.StartNew();
    protected Stopwatch StartWatch => _startWatch;

    //TickTimeSamples number of per-tick time samples, maps to the 100 samples of vanilla tickTimesNanos
    public const int TickTimeSamples = 100;

    //NanosecondsPerTimestampTick conversion factor from stopwatch ticks to nanoseconds
    private static readonly double NanosecondsPerTimestampTick = 1_000_000_000.0 / Stopwatch.Frequency;

    //_tickTimesNanos ring buffer of recent per-tick times, for /debug tick query percentiles
    private readonly long[] _tickTimesNanos = new long[TickTimeSamples];
    private int _tickTimeCursor;
    private int _tickTimeFilled;

    //TickRate tick rate management, maps to vanilla tickRateManager, operated by /debug tick
    public ServerTickRateManager TickRate { get; } = new();

    //Functions function manager, maps to vanilla MinecraftServer.functionManager
    //Assembled by the concrete implementation at startup; both function loading and the tick/load tags go through it
    public ServerFunctionManager Functions { get; protected set; } = null!;

    //ScheduledEvents scheduled event queue, maps to vanilla MinecraftServer.scheduledEvents
    //Stored in data/minecraft/scheduled_events.dat, the read/write source of /schedule
    public TimerQueue<MinecraftServer> ScheduledEvents { get; protected set; } = null!;

    //The following are the shared server state contract provided by the concrete implementation (DedicatedServer or a future built-in IntegratedServer)
    //Vanilla defines these members on the MinecraftServer base class anyway; the command layer and shared business only depend on the base class
    //So when sinking the dedicated-specific parts into a separate server library, the shared layer needs no change

    //Settings server configuration, maps to vanilla server.properties
    public abstract ServerSettings Settings { get; }
    //Overworld the overworld level
    public abstract PersistentServerLevel Overworld { get; }
    //GetLevel gets a level by dimension key; returns null when absent
    public abstract PersistentServerLevel? GetLevel(ResourceKey<Level> key);
    //PlayerList online player list
    public abstract PlayerList PlayerList { get; }
    //Connections connected connections
    public abstract IReadOnlyList<Connection> Connections { get; }
    //ClockManager world clock manager
    public abstract ServerClockManager ClockManager { get; }
    //BlockEntities block entity collection
    public abstract BlockEntityManager BlockEntities { get; }
    //CommandStorage command storage
    public abstract CommandStorage CommandStorage { get; }
    //Stopwatches debug stopwatch collection
    public abstract Stopwatches Stopwatches { get; }
    //DebugPlayers fake player manager
    public abstract DebugPlayerManager DebugPlayers { get; }
    //Commands command manager
    public abstract CommandManager Commands { get; }
    //EntityTracker entity tracker
    public abstract EntityTracker EntityTracker { get; }
    //WorldSeed world seed
    public abstract long WorldSeed { get; }
    //LevelData level metadata
    public abstract LevelData LevelData { get; }
    //PlayerData player data storage
    public abstract PlayerDataStorage PlayerData { get; }
    //GameRules game rules
    public abstract GameRuleMapData GameRules { get; }
    //OpList operator list
    public abstract OpList OpList { get; }
    //BanList player ban list
    public abstract BanList BanList { get; }
    //IpBanList IP ban list
    public abstract IpBanList IpBanList { get; }
    //WhiteList whitelist
    public abstract WhiteList WhiteList { get; }
    //IsWhiteListEnabled whitelist toggle
    public abstract bool IsWhiteListEnabled { get; set; }
    //SpawnPos world spawn point
    public abstract Vec3 SpawnPos { get; }
    //SetSpawnPos sets the world spawn point
    public abstract void SetSpawnPos(Vec3 pos);
    //DefaultGameType default game type
    public abstract GameType DefaultGameType { get; }
    //SetDefaultGameType sets the default game type
    public abstract void SetDefaultGameType(GameType gameType);
    //SetWeatherParameters sets the weather, maps to vanilla setWeatherParameters
    public abstract void SetWeatherParameters(int clearTime, int rainTime, bool raining, bool thundering);
    //IsSavingEnabled whether saving is allowed
    public abstract bool IsSavingEnabled { get; }
    //SetSavingEnabled toggles saving
    public abstract void SetSavingEnabled(bool enabled);
    //SaveAllNow flushes everything to disk immediately
    public abstract void SaveAllNow();

    //AverageTickTimeNanos average per-tick time of recent samples
    public long AverageTickTimeNanos
        => _tickTimeFilled == 0 ? 0 : (long)_tickTimesNanos.Take(_tickTimeFilled).Average();

    //TickTimesNanos a copy of the collected tick time samples, for percentile queries; callers sort so the internal array must not be handed out
    public IReadOnlyList<long> TickTimesNanos => _tickTimesNanos.Take(_tickTimeFilled).ToArray();

    //RecordTickTime records this tick's time into the ring buffer
    private void RecordTickTime(long elapsedNanos)
    {
        _tickTimesNanos[_tickTimeCursor] = elapsedNanos;
        _tickTimeCursor = (_tickTimeCursor + 1) % TickTimeSamples;
        if (_tickTimeFilled < TickTimeSamples) _tickTimeFilled++;
    }

    protected MinecraftServer(Thread serverThread)
    {
        _serverThread = serverThread;
    }

    //Running whether it is in the main loop
    public bool Running => _running;

    //TickCount accumulated Tick count, for diagnostics
    public long TickCount => _tickCount;

    //ServerThread the main loop thread, for diagnostics
    public Thread ServerThread => _serverThread;

    //WorldGate world state gate, for a subclass to mutually exclude a full flush from the whole main loop tick when on a non-main-loop thread
    //Reentry on the same thread is allowed; a command-triggered shutdown runs on the main loop thread
    protected object WorldGate => _worldGate;

    //SleepBudgetMillis per-tick sleep budget; tests set 0 to speed up, production uses the default 50ms
    public int SleepBudgetMillis
    {
        get => _sleepBudgetMillis;
        set => _sleepBudgetMillis = Math.Max(0, value);
    }

    //Run main loop entry, blocks the calling thread until Stop is called
    //Aligned with vanilla runServer: nextTickTime accumulates the target time, exceeding the threshold logs a Can't keep up warning, then sleeps to the target time
    //On a tick exception it falls back to the Stop flush path, maps to vanilla runServer calling stopServer after an exception
    public void Run()
    {
        if (_running) return;
        _running = true;
        //Stop may have run before the main loop started; that round's _running=false would be overwritten by the line above
        //Re-confirms the cancel flag here so such shutdown requests are not lost; a pipeline script feeding stop on entry goes through this
        if (_shutdownCts.IsCancellationRequested)
        {
            _running = false;
            Log.Info("MinecraftServer stop was requested before the main loop started, not entering the loop");
            return;
        }
        //Pins the main thread; the watchdog relies on it to tell "the call that blocked the main thread"
        Log.SetMainThread(Environment.CurrentManagedThreadId);
        Log.Info("MinecraftServer main loop started");
        var now = _tickClock.ElapsedMilliseconds;
        _nextTickTimeMillis = now;
        _lastOverloadWarningMillis = now;
        try
        {
            while (_running)
            {
                //First let the tick rate manager settle this tick; when frozen with no pending step ticks RunsNormally is false and the subclass Tick skips world advancement
                TickRate.Tick();
                //The beat is derived from ticks per second; when sprinting the result is 0 and it does not sleep
                //The budget only throttles when lowered below the 20-tick beat, to speed up regression tests; production's default 50 does not limit
                var tickMillis = TickRate.TickMillis;
                if (_sleepBudgetMillis <= 0) tickMillis = 0;
                else if (_sleepBudgetMillis < TargetTickMillis) tickMillis = Math.Min(tickMillis, _sleepBudgetMillis);
                now = _tickClock.ElapsedMilliseconds;
                if (tickMillis <= 0)
                {
                    //A budget of 0 is the sprint test; the timeline resets each tick and overload is not checked, maps to vanilla's sprint branch
                    _nextTickTimeMillis = now;
                    _lastOverloadWarningMillis = now;
                }
                else
                {
                    var behind = now - _nextTickTimeMillis;
                    //Lagging over the one-second threshold adds 20 ticks and only reports when long enough since the last warning, maps to vanilla runServer's check
                    if (behind > OverloadedThresholdMillis + 20 * tickMillis
                        && _nextTickTimeMillis - _lastOverloadWarningMillis
                            >= OverloadedWarningIntervalMillis + 100 * tickMillis)
                    {
                        var ticks = behind / tickMillis;
                        //Take the position out before logging, or you would capture this diagnostic itself
                        var originMember = Log.LastOriginMember;
                        var originFile = Log.LastOriginFile;
                        var originLine = Log.LastOriginLine;
                        var originSource = Log.LastOriginSource;
                        Log.Warning($"Can't keep up! Is the server overloaded? Running {behind}ms or {ticks} ticks behind");
                        //Bring out the call location of the main thread's last log, so where the main thread last stopped before the stall is clear
                        Log.Debug($"Last main-thread call site before Can't keep up {originSource} {originMember} {originFile}:{originLine}");
                        //Shift the whole timeline forward to erase the already warned lag, avoiding repeated accumulation
                        _nextTickTimeMillis += ticks * tickMillis;
                        _lastOverloadWarningMillis = _nextTickTimeMillis;
                    }
                    _nextTickTimeMillis += tickMillis;
                }
                var tickStartTimestamp = Stopwatch.GetTimestamp();
                try
                {
                    //Hold the gate for the whole tick: a shutdown flush either waits for this tick to finish or this tick does not run at all
                    //Lock per tick rather than per cell, so the snapshot cannot capture the half-state of "block changed but block entity not registered"
                    //With no shutdown there is no contention and the lock overhead is negligible
                    lock (_worldGate)
                    {
                        //A command posted from the console shares the same thread and gate as this tick; the command sees a consistent world state at the tick boundary
                        var consoleStart = TickStageProfiler.Now();
                        DrainConsoleCommands();
                        TickStageProfiler.Record(TickStage.Console, consoleStart);
                        var tickBodyStart = TickStageProfiler.Now();
                        Tick();
                        TickStageProfiler.Record(TickStage.TickTotal, tickBodyStart);
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Server tick exception, main loop exiting {e}");
                    //The crash scene goes through the crash report landing in the program root crash-reports/, the same path as the client, as vanilla does
                    CrashHandler.Handle(e, "server Tick exception");
                    Log.Flush();
                    try { Stop(); }
                    catch (Exception stopEx) { Log.Error($"Exception stop flush failed {stopEx}"); }
                    break;
                }
                //This tick's measured time goes into the sample ring and the sprint stats, for /debug tick query
                var elapsedNanos = (long)((Stopwatch.GetTimestamp() - tickStartTimestamp) * NanosecondsPerTimestampTick);
                RecordTickTime(elapsedNanos);
                TickStageProfiler.EndTick();
                TickRate.EndTickWork(elapsedNanos);
                _tickCount++;
                if (tickMillis > 0)
                {
                    //waitUntilNextTick sleeps until the target time; when lagging the value is negative and it goes straight to the next tick
                    var remaining = _nextTickTimeMillis - _tickClock.ElapsedMilliseconds;
                    if (remaining > 0) Thread.Sleep((int)remaining);
                }
            }
        }
        finally
        {
            _running = false;
            Log.Info($"MinecraftServer main loop exited after {_tickCount} ticks");
        }
    }

    //Tick the single-frame logic base skeleton; subclasses override as needed
    //Vanilla Tick includes world advancement/player scheduling/network handling; this is only the skeleton
    protected virtual void Tick()
    {
    }

    //EnqueueConsoleCommand posts a console command, taken by the next main loop to execute
    //Both the console thread and the GUI thread go through this, maps to vanilla handleConsoleInput which only queues
    public void EnqueueConsoleCommand(CommandSourceStack source, string command)
        => _consoleCommands.Enqueue((source, command, null));

    //ExecuteBlocking runs a command on the main loop thread and blocks until done, maps to vanilla executeBlocking
    //Callers such as RCON that need a synced reply go through it; the command shares the queue with console commands and sees the world state at the same tick boundary
    //When the main loop has exited the result never comes; the caller thread escapes via the GenericThread.Stop interrupt
    public void ExecuteBlocking(CommandSourceStack source, string command)
    {
        var done = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _consoleCommands.Enqueue((source, command, done));
        done.Task.Wait();
    }

    //DrainConsoleCommands takes and runs commands posted from the console
    //An exception thrown by the command body must not take down the main loop, consistent with the fallback on the console thread
    private void DrainConsoleCommands()
    {
        while (_consoleCommands.TryDequeue(out var entry))
        {
            try
            {
                Commands.Execute(entry.Source, entry.Command);
            }
            catch (Exception e)
            {
                Log.Error($"Console command failed {entry.Command}: {e}");
            }
            finally
            {
                entry.Done?.TrySetResult(null);
            }
        }
    }

    //Stop triggers the main loop exit, called externally or by the ShutdownHook
    //A subclass override must call base.Stop to keep _running and Cts state
    public virtual void Stop()
    {
        Log.Info("MinecraftServer received stop signal");
        _running = false;
        _shutdownCts.Cancel();
    }

    //WaitForShutdown blocks the calling thread until the server shuts down; an external EXE uses it to keep the process alive
    public void WaitForShutdown()
    {
        try
        {
            _shutdownCts.Token.WaitHandle.WaitOne();
        }
        catch (OperationCanceledException)
        {
            //Normal exit
        }
    }
}
