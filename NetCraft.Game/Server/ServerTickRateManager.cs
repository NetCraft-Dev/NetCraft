using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Logging;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Server;

//ServerTickRateManager server-side tick rate management, maps to vanilla net.minecraft.server.ServerTickRateManager
//Manages four things: ticks per second / freezing the world / stepping while frozen / sprinting
//The main loop calls Tick at the start of each tick to decide whether the world advances, and EndTickWork at the end to accumulate sprint time
public sealed class ServerTickRateManager
{
    //MinTickRate min ticks per second, maps to vanilla TickRateManager.MIN_TICKRATE
    public const float MinTickRate = 1f;

    //DefaultTickRate default ticks per second, maps to vanilla 20
    public const float DefaultTickRate = 20f;

    //NanosecondsPerSecond nanoseconds in one second
    private const long NanosecondsPerSecond = 1_000_000_000L;

    private float _tickRate = DefaultTickRate;
    private long _nanosecondsPerTick = NanosecondsPerSecond / 20;
    private int _frozenTicksToRun;
    private bool _isFrozen;
    private bool _runGameElements = true;
    private long _remainingSprintTicks;
    private long _scheduledSprintTicks;
    private long _sprintTimeSpentNanos;
    private bool _frozenBeforeSprint;

    //Players online player set; after injection state changes sync to the client, otherwise only local state changes
    public PlayerList? Players { get; set; }

    //TickRate ticks per second
    public float TickRate => _tickRate;

    //NanosecondsPerTick per-tick target duration
    public long NanosecondsPerTick => _nanosecondsPerTick;

    //MillisecondsPerTick per-tick target in milliseconds
    public float MillisecondsPerTick => _nanosecondsPerTick / 1_000_000f;

    //TickMillis main loop beat in milliseconds; no sleep while sprinting
    public int TickMillis => IsSprinting ? 0 : (int)(_nanosecondsPerTick / 1_000_000L);

    //IsFrozen whether the world is frozen
    public bool IsFrozen => _isFrozen;

    //IsSprinting whether sprinting
    public bool IsSprinting => _scheduledSprintTicks > 0;

    //IsSteppingForward whether stepping
    public bool IsSteppingForward => _frozenTicksToRun > 0;

    //FrozenTicksToRun remaining step ticks
    public int FrozenTicksToRun => _frozenTicksToRun;

    //SprintTicksRemaining remaining sprint ticks
    public long SprintTicksRemaining => _remainingSprintTicks;

    //RunsNormally whether this tick advances world elements
    public bool RunsNormally => _runGameElements;

    //SendStateToJoiningPlayer resends the current tick rate and step state when a new player enters the world, maps to vanilla updateJoiningPlayer
    //Without it the client does not know whether the world is frozen and the local world advances on its own
    public void SendStateToJoiningPlayer(ServerPlayer player)
    {
        player.Connection.Send(new ClientboundTickingStatePacket(_tickRate, _isFrozen));
        player.Connection.Send(new ClientboundTickingStepPacket(_frozenTicksToRun));
    }

    //SetTickRate sets ticks per second, maps to vanilla setTickRate
    public void SetTickRate(float rate)
    {
        _tickRate = Math.Max(rate, MinTickRate);
        _nanosecondsPerTick = (long)(NanosecondsPerSecond / _tickRate);
        BroadcastState();
    }

    //SetFrozen freezes or unfreezes the world, maps to vanilla setFrozen
    public void SetFrozen(bool frozen)
    {
        _isFrozen = frozen;
        BroadcastState();
    }

    //StepGameIfPaused advances the given ticks while frozen; returns false when not frozen, maps to vanilla stepGameIfPaused
    public bool StepGameIfPaused(int ticks)
    {
        if (!_isFrozen) return false;
        _frozenTicksToRun = ticks;
        BroadcastStep();
        return true;
    }

    //StopStepping stops stepping, maps to vanilla stopStepping
    public bool StopStepping()
    {
        if (_frozenTicksToRun <= 0) return false;
        _frozenTicksToRun = 0;
        BroadcastStep();
        return true;
    }

    //RequestGameToSprint requests a sprint of the given ticks, maps to vanilla requestGameToSprint
    //Returns true when already running, meaning this request interrupted the previous one; a sprint unfreezes first and restores the prior frozen state afterwards
    public bool RequestGameToSprint(int ticks)
    {
        var interrupted = _remainingSprintTicks > 0;
        _sprintTimeSpentNanos = 0;
        _scheduledSprintTicks = ticks;
        _remainingSprintTicks = ticks;
        _frozenBeforeSprint = _isFrozen;
        SetFrozen(false);
        return interrupted;
    }

    //StopSprinting ends a sprint early, maps to vanilla stopSprinting
    public bool StopSprinting()
    {
        if (_remainingSprintTicks <= 0) return false;
        FinishTickSprint();
        return true;
    }

    //Tick called at the start of each tick, maps to vanilla TickRateManager.tick and checkShouldSprintThisTick
    //When frozen with no pending step ticks this tick does not advance the world; the step and sprint counters decrement here
    public void Tick()
    {
        _runGameElements = !_isFrozen || _frozenTicksToRun > 0;
        if (_frozenTicksToRun > 0) _frozenTicksToRun--;
        if (_scheduledSprintTicks <= 0) return;
        if (!_runGameElements) return;
        if (_remainingSprintTicks > 0) _remainingSprintTicks--;
        else FinishTickSprint();
    }

    //EndTickWork at the end of the tick accumulates this tick's measured time into the sprint stats, maps to vanilla endTickWork
    public void EndTickWork(long elapsedNanos)
    {
        if (_scheduledSprintTicks > 0) _sprintTimeSpentNanos += elapsedNanos;
    }

    //FinishTickSprint finishes a sprint: computes the average ticks per second and per-tick milliseconds and broadcasts a report, maps to vanilla finishTickSprint
    private void FinishTickSprint()
    {
        var completed = _scheduledSprintTicks - _remainingSprintTicks;
        //Vanilla floors the accumulated nanoseconds to 1 before converting to milliseconds, avoiding a zero duration overflowing ticks per second
        var spentMillis = Math.Max(1.0, (double)_sprintTimeSpentNanos) / 1_000_000.0;
        var ticksPerSecond = (int)(1000.0 * completed / spentMillis);
        var millisecondsPerTick = completed == 0 ? MillisecondsPerTick : (float)(spentMillis / completed);
        _scheduledSprintTicks = 0;
        _remainingSprintTicks = 0;
        _sprintTimeSpentNanos = 0;
        SetFrozen(_frozenBeforeSprint);
            var text = $"sprint finished {completed} ticks, {ticksPerSecond} tps, {millisecondsPerTick:F2} ms/tick";
        Log.Info(text);
        Players?.BroadcastSystemMessage(Component.Literal(text), false);
    }

    //BroadcastState syncs the tick rate and frozen state to the client, maps to vanilla updateStateToClients
    private void BroadcastState()
        => Players?.BroadcastAll(new ClientboundTickingStatePacket(_tickRate, _isFrozen));

    //BroadcastStep syncs the pending step ticks to the client, maps to vanilla updateStepTicks
    private void BroadcastStep()
        => Players?.BroadcastAll(new ClientboundTickingStepPacket(_frozenTicksToRun));
}
