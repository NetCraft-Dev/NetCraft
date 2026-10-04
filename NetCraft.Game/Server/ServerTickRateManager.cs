using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Logging;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Server;

//ServerTickRateManager 服务端刻速率管理对应原版 net.minecraft.server.ServerTickRateManager
//管四件事: 每秒刻数 / 冻结世界 / 冻结下步进 / 加速跑
//主循环每拍开头调 Tick 决定这一拍推不推进世界 拍尾调 EndTickWork 累计加速跑耗时
public sealed class ServerTickRateManager
{
    //MinTickRate 最低每秒刻数 对应原版 TickRateManager.MIN_TICKRATE
    public const float MinTickRate = 1f;

    //DefaultTickRate 默认每秒刻数 对应原版 20
    public const float DefaultTickRate = 20f;

    //NanosecondsPerSecond 一秒的纳秒数
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

    //Players 在线玩家集合 注入后状态变化会同步给客户端 未注入时只改本地状态
    public PlayerList? Players { get; set; }

    //TickRate 每秒刻数
    public float TickRate => _tickRate;

    //NanosecondsPerTick 单刻目标耗时
    public long NanosecondsPerTick => _nanosecondsPerTick;

    //MillisecondsPerTick 单刻目标毫秒
    public float MillisecondsPerTick => _nanosecondsPerTick / 1_000_000f;

    //TickMillis 主循环节拍毫秒 加速跑时不睡
    public int TickMillis => IsSprinting ? 0 : (int)(_nanosecondsPerTick / 1_000_000L);

    //IsFrozen 世界是否冻结
    public bool IsFrozen => _isFrozen;

    //IsSprinting 是否在加速跑
    public bool IsSprinting => _scheduledSprintTicks > 0;

    //IsSteppingForward 是否在步进
    public bool IsSteppingForward => _frozenTicksToRun > 0;

    //FrozenTicksToRun 剩余步进刻数
    public int FrozenTicksToRun => _frozenTicksToRun;

    //SprintTicksRemaining 剩余加速刻数
    public long SprintTicksRemaining => _remainingSprintTicks;

    //RunsNormally 这一拍是否推进世界元素
    public bool RunsNormally => _runGameElements;

    //SendStateToJoiningPlayer 新玩家进世界时补发当前刻速率与步进状态 对应原版 updateJoiningPlayer
    //不补发的话客户端不知道世界当前是否冻结 本地世界会自顾自地推进
    public void SendStateToJoiningPlayer(ServerPlayer player)
    {
        player.Connection.Send(new ClientboundTickingStatePacket(_tickRate, _isFrozen));
        player.Connection.Send(new ClientboundTickingStepPacket(_frozenTicksToRun));
    }

    //SetTickRate 设置每秒刻数 对应原版 setTickRate
    public void SetTickRate(float rate)
    {
        _tickRate = Math.Max(rate, MinTickRate);
        _nanosecondsPerTick = (long)(NanosecondsPerSecond / _tickRate);
        BroadcastState();
    }

    //SetFrozen 冻结或解冻世界 对应原版 setFrozen
    public void SetFrozen(bool frozen)
    {
        _isFrozen = frozen;
        BroadcastState();
    }

    //StepGameIfPaused 冻结状态下推进指定刻数 未冻结返回 false 对应原版 stepGameIfPaused
    public bool StepGameIfPaused(int ticks)
    {
        if (!_isFrozen) return false;
        _frozenTicksToRun = ticks;
        BroadcastStep();
        return true;
    }

    //StopStepping 停止步进 对应原版 stopStepping
    public bool StopStepping()
    {
        if (_frozenTicksToRun <= 0) return false;
        _frozenTicksToRun = 0;
        BroadcastStep();
        return true;
    }

    //RequestGameToSprint 请求加速跑指定刻数 对应原版 requestGameToSprint
    //已在跑时返回 true 表示这次请求把上一次打断了 加速跑会先解冻并在结束后恢复原冻结状态
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

    //StopSprinting 提前结束加速跑 对应原版 stopSprinting
    public bool StopSprinting()
    {
        if (_remainingSprintTicks <= 0) return false;
        FinishTickSprint();
        return true;
    }

    //Tick 每拍开头调 对应原版 TickRateManager.tick 与 checkShouldSprintThisTick
    //冻结且没有待步进刻数时本拍不推进世界 步进与加速计数在这里递减
    public void Tick()
    {
        _runGameElements = !_isFrozen || _frozenTicksToRun > 0;
        if (_frozenTicksToRun > 0) _frozenTicksToRun--;
        if (_scheduledSprintTicks <= 0) return;
        if (!_runGameElements) return;
        if (_remainingSprintTicks > 0) _remainingSprintTicks--;
        else FinishTickSprint();
    }

    //EndTickWork 拍尾把本拍实测耗时累计进加速跑统计 对应原版 endTickWork
    public void EndTickWork(long elapsedNanos)
    {
        if (_scheduledSprintTicks > 0) _sprintTimeSpentNanos += elapsedNanos;
    }

    //FinishTickSprint 加速跑收尾 算平均每秒刻数与每刻毫秒并广播报告 对应原版 finishTickSprint
    private void FinishTickSprint()
    {
        var completed = _scheduledSprintTicks - _remainingSprintTicks;
        //原版先把累计纳秒下限到 1 再换算毫秒 避免零耗时让每秒刻数溢出
        var spentMillis = Math.Max(1.0, (double)_sprintTimeSpentNanos) / 1_000_000.0;
        var ticksPerSecond = (int)(1000.0 * completed / spentMillis);
        var millisecondsPerTick = completed == 0 ? MillisecondsPerTick : (float)(spentMillis / completed);
        _scheduledSprintTicks = 0;
        _remainingSprintTicks = 0;
        _sprintTimeSpentNanos = 0;
        SetFrozen(_frozenBeforeSprint);
        var text = $"加速跑结束 {completed} 刻 {ticksPerSecond} 刻/秒 每刻 {millisecondsPerTick:F2} ms";
        Log.Info(text);
        Players?.BroadcastSystemMessage(Component.Literal(text), false);
    }

    //BroadcastState 把刻速率与冻结状态同步给客户端 对应原版 updateStateToClients
    private void BroadcastState()
        => Players?.BroadcastAll(new ClientboundTickingStatePacket(_tickRate, _isFrozen));

    //BroadcastStep 把待步进刻数同步给客户端 对应原版 updateStepTicks
    private void BroadcastStep()
        => Players?.BroadcastAll(new ClientboundTickingStepPacket(_frozenTicksToRun));
}
