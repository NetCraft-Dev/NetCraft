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

//MinecraftServer 服务端主循环对应原版 net.minecraft.server.MinecraftServer
//持有运行状态与 Shutdown 信号提供 Tick 与 Run 主循环骨架
//阶段 11.35 接入循环空壳阶段 11.46 加 tick 间隔控制避免 CPU 100% 对齐原版 20 TPS
//子类 DedicatedServer 接入 PersistentServerLevel 真实存档调度
public abstract class MinecraftServer
{
    //TargetTps 目标每秒 tick 数对齐原版 20 TPS
    public const int TargetTps = 20;
    //TargetTickMillis 单 tick 目标时长 50ms
    public const int TargetTickMillis = 1000 / TargetTps;

    //OverloadedThresholdMillis 落后超过该值才判过载 对齐原版 OVERLOADED_THRESHOLD_NANOS 即 1 秒
    public const long OverloadedThresholdMillis = 1000;
    //OverloadedWarningIntervalMillis 两次过载告警最小间隔 对齐原版 OVERLOADED_WARNING_INTERVAL_NANOS 即 10 秒
    public const long OverloadedWarningIntervalMillis = 10000;

    private readonly Thread _serverThread;
    private volatile bool _running;
    private long _tickCount;
    private readonly CancellationTokenSource _shutdownCts = new();
    //_worldGate 世界状态门 主循环的每一拍与关服刷盘互斥
    //原版 stopServer 是提交到 server 线程执行的 与 tick 天然串行
    //本作 Stop 可能从 Ctrl+C 或 GUI 线程进来 不串行就会在搬运活塞的半途取快照
    //那种快照里方块已经是移动活塞而方块实体还没登记 读回来那格永久卡住
    private readonly object _worldGate = new();
    //_consoleCommands 控制台投递进来的命令 只入队 由主循环取出执行
    //对应原版 DedicatedServer 的 serverCommandQueue 与 handleConsoleInputs
    //命令行线程直接把命令跑掉的话 世界状态会与主循环交叉 主循环持门时命令也拿不到一致快照
    //Done 给要同步回包的调用方(RCON)用 主循环执行完放行阻塞者 普通投递为 null
    private readonly ConcurrentQueue<(CommandSourceStack Source, string Command, TaskCompletionSource<object?>? Done)> _consoleCommands = new();
    //SleepBudgetMillis 单 tick sleep 预算小于 1 时不 sleep 避免 CPU 100%
    private int _sleepBudgetMillis = TargetTickMillis;
    //_tickClock 主循环时间线 单调递增用于 nextTickTime 落后量计算
    private readonly Stopwatch _tickClock = Stopwatch.StartNew();
    //_nextTickTimeMillis 下一次 tick 的目标起始时刻 落后量由它推算对应原版 nextTickTimeNanos
    private long _nextTickTimeMillis;
    //_lastOverloadWarningMillis 上次过载告警的时间线位置 用于抑制告警频率
    private long _lastOverloadWarningMillis;
    //StartWatch 服务端启动计时构造时开始对应原版 server 启动时刻供 Done 日志
    private readonly Stopwatch _startWatch = Stopwatch.StartNew();
    protected Stopwatch StartWatch => _startWatch;

    //TickTimeSamples 单拍耗时样本数 对应原版 tickTimesNanos 的 100 个采样
    public const int TickTimeSamples = 100;

    //NanosecondsPerTimestampTick 秒表刻度到纳秒的换算系数
    private static readonly double NanosecondsPerTimestampTick = 1_000_000_000.0 / Stopwatch.Frequency;

    //_tickTimesNanos 最近若干拍的耗时环形缓冲 供 /debug tick query 算分位数
    private readonly long[] _tickTimesNanos = new long[TickTimeSamples];
    private int _tickTimeCursor;
    private int _tickTimeFilled;

    //TickRate 刻速率管理对应原版 tickRateManager 由 /debug tick 操作
    public ServerTickRateManager TickRate { get; } = new();

    //Functions 函数管理器对应原版 MinecraftServer.functionManager
    //由具体实现启动时装配 函数加载与 tick/load 标签都经它
    public ServerFunctionManager Functions { get; protected set; } = null!;

    //ScheduledEvents 计划事件队列对应原版 MinecraftServer.scheduledEvents
    //存 data/minecraft/scheduled_events.dat /schedule 的读写来源
    public TimerQueue<MinecraftServer> ScheduledEvents { get; protected set; } = null!;

    //以下为服务端共享状态契约 由具体实现(DedicatedServer 或将来内置的 IntegratedServer)提供
    //原版这些成员本就定义在 MinecraftServer 基类上 命令层与共享业务只依赖基类
    //这样把 dedicated 专属部分下沉到独立服务端库时 共享层一行都不用改

    //Settings 服务端配置对应原版 server.properties
    public abstract ServerSettings Settings { get; }
    //Overworld 主世界关卡
    public abstract PersistentServerLevel Overworld { get; }
    //GetLevel 按维度键取关卡 不存在返回 null
    public abstract PersistentServerLevel? GetLevel(ResourceKey<Level> key);
    //PlayerList 在线玩家名单
    public abstract PlayerList PlayerList { get; }
    //Connections 已接入的连接
    public abstract IReadOnlyList<Connection> Connections { get; }
    //ClockManager 世界时钟管理器
    public abstract ServerClockManager ClockManager { get; }
    //BlockEntities 方块实体集合
    public abstract BlockEntityManager BlockEntities { get; }
    //CommandStorage 命令存储
    public abstract CommandStorage CommandStorage { get; }
    //Stopwatches 调试计时器集合
    public abstract Stopwatches Stopwatches { get; }
    //DebugPlayers 假玩家管理器
    public abstract DebugPlayerManager DebugPlayers { get; }
    //Commands 命令管理器
    public abstract CommandManager Commands { get; }
    //EntityTracker 实体追踪器
    public abstract EntityTracker EntityTracker { get; }
    //WorldSeed 世界种子
    public abstract long WorldSeed { get; }
    //LevelData 关卡元数据
    public abstract LevelData LevelData { get; }
    //PlayerData 玩家数据存档
    public abstract PlayerDataStorage PlayerData { get; }
    //GameRules 游戏规则
    public abstract GameRuleMapData GameRules { get; }
    //OpList 管理员名单
    public abstract OpList OpList { get; }
    //BanList 玩家封禁名单
    public abstract BanList BanList { get; }
    //IpBanList IP 封禁名单
    public abstract IpBanList IpBanList { get; }
    //WhiteList 白名单
    public abstract WhiteList WhiteList { get; }
    //IsWhiteListEnabled 白名单开关
    public abstract bool IsWhiteListEnabled { get; set; }
    //SpawnPos 世界出生点
    public abstract Vec3 SpawnPos { get; }
    //SetSpawnPos 设置世界出生点
    public abstract void SetSpawnPos(Vec3 pos);
    //DefaultGameType 默认游戏模式
    public abstract GameType DefaultGameType { get; }
    //SetDefaultGameType 设置默认游戏模式
    public abstract void SetDefaultGameType(GameType gameType);
    //SetWeatherParameters 设置天气对应原版 setWeatherParameters
    public abstract void SetWeatherParameters(int clearTime, int rainTime, bool raining, bool thundering);
    //IsSavingEnabled 是否允许存档
    public abstract bool IsSavingEnabled { get; }
    //SetSavingEnabled 开关存档
    public abstract void SetSavingEnabled(bool enabled);
    //SaveAllNow 立即全量存盘
    public abstract void SaveAllNow();

    //AverageTickTimeNanos 最近样本的平均单拍耗时
    public long AverageTickTimeNanos
        => _tickTimeFilled == 0 ? 0 : (long)_tickTimesNanos.Take(_tickTimeFilled).Average();

    //TickTimesNanos 已采集的耗时样本副本 供查询分位数 调用方会排序不能直接把内部数组给出去
    public IReadOnlyList<long> TickTimesNanos => _tickTimesNanos.Take(_tickTimeFilled).ToArray();

    //RecordTickTime 记录本拍耗时进环形缓冲
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

    //Running 是否在主循环中
    public bool Running => _running;

    //TickCount 累计 Tick 数用于诊断
    public long TickCount => _tickCount;

    //ServerThread 主循环所在线程用于诊断
    public Thread ServerThread => _serverThread;

    //WorldGate 世界状态门 供子类在非主循环线程做全量刷盘时与主循环整拍互斥
    //同一线程重入是允许的 命令触发的关服就在主循环线程里跑
    protected object WorldGate => _worldGate;

    //SleepBudgetMillis 单 tick sleep 预算测试场景设 0 加速跑测生产用默认 50ms
    public int SleepBudgetMillis
    {
        get => _sleepBudgetMillis;
        set => _sleepBudgetMillis = Math.Max(0, value);
    }

    //Run 主循环入口阻塞调用线程直到 Stop 被调用
    //对齐原版 runServer: nextTickTime 累积目标时刻 落后超阈值打 Can't keep up 告警 再睡到目标时刻
    //tick 异常时走 Stop 刷盘路径兜底对应原版 runServer 异常后调 stopServer
    public void Run()
    {
        if (_running) return;
        _running = true;
        //Stop 可能赶在主循环起步前就跑完了 那一轮的 _running=false 会被上面这行盖掉
        //这里回头确认一次取消标志 保证那类关闭请求不丢 管道脚本一进来就喂 stop 走的就是这条
        if (_shutdownCts.IsCancellationRequested)
        {
            _running = false;
            Log.Info("MinecraftServer stop was requested before the main loop started, not entering the loop");
            return;
        }
        //圈定主线程 看门狗要靠它区分"卡住主线程的那一次调用"
        Log.SetMainThread(Environment.CurrentManagedThreadId);
        Log.Info("MinecraftServer main loop started");
        var now = _tickClock.ElapsedMilliseconds;
        _nextTickTimeMillis = now;
        _lastOverloadWarningMillis = now;
        try
        {
            while (_running)
            {
                //先让刻速率管理结算这一拍 冻结且无待步进刻数时 RunsNormally 为假 子类 Tick 会跳过世界推进
                TickRate.Tick();
                //节拍由每秒刻数推导 加速跑时推导结果为 0 不睡
                //预算只在被调到低于 20 刻节拍时起限速作用 供回归测试加速 生产默认 50 不参与限制
                var tickMillis = TickRate.TickMillis;
                if (_sleepBudgetMillis <= 0) tickMillis = 0;
                else if (_sleepBudgetMillis < TargetTickMillis) tickMillis = Math.Min(tickMillis, _sleepBudgetMillis);
                now = _tickClock.ElapsedMilliseconds;
                if (tickMillis <= 0)
                {
                    //预算 0 是加速跑测 时间线每 tick 重置且不判过载 对应原版 sprint 分支
                    _nextTickTimeMillis = now;
                    _lastOverloadWarningMillis = now;
                }
                else
                {
                    var behind = now - _nextTickTimeMillis;
                    //落后超过阈值一秒加 20 tick 且距上次告警够久才报 对应原版 runServer 判定
                    if (behind > OverloadedThresholdMillis + 20 * tickMillis
                        && _nextTickTimeMillis - _lastOverloadWarningMillis
                            >= OverloadedWarningIntervalMillis + 100 * tickMillis)
                    {
                        var ticks = behind / tickMillis;
                        //先把位置取出来再打日志 否则取到的就是这条诊断自己
                        var originMember = Log.LastOriginMember;
                        var originFile = Log.LastOriginFile;
                        var originLine = Log.LastOriginLine;
                        var originSource = Log.LastOriginSource;
                        Log.Warning($"Can't keep up! Is the server overloaded? Running {behind}ms or {ticks} ticks behind");
                        //把主线程最近一条日志的调用位置带出来 卡顿前主线程最后停在哪一目了然
                        Log.Debug($"Last main-thread call site before Can't keep up {originSource} {originMember} {originFile}:{originLine}");
                        //把时间线整体前移抹掉已告警的落后量 避免重复累计
                        _nextTickTimeMillis += ticks * tickMillis;
                        _lastOverloadWarningMillis = _nextTickTimeMillis;
                    }
                    _nextTickTimeMillis += tickMillis;
                }
                var tickStartTimestamp = Stopwatch.GetTimestamp();
                try
                {
                    //整拍持门: 关服刷盘要么等这一拍跑完 要么这一拍整个不跑
                    //按拍加锁而不是按格加锁 快照就取不到"方块已换而方块实体没登记"的半成品
                    //没有关服时全程无争用 锁本身的开销可忽略
                    lock (_worldGate)
                    {
                        //控制台投进来的命令与这一拍同线程同门 命令看到的是整拍边界上的一致世界状态
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
                    //崩溃现场走崩溃报告 落程序根的 crash-reports/ 与客户端同一条路 原版也是这样
                    CrashHandler.Handle(e, "服务端 Tick 异常");
                    Log.Flush();
                    try { Stop(); }
                    catch (Exception stopEx) { Log.Error($"Exception stop flush failed {stopEx}"); }
                    break;
                }
                //本拍实测耗时进样本环与加速跑统计 供 /debug tick query 显示
                var elapsedNanos = (long)((Stopwatch.GetTimestamp() - tickStartTimestamp) * NanosecondsPerTimestampTick);
                RecordTickTime(elapsedNanos);
                TickStageProfiler.EndTick();
                TickRate.EndTickWork(elapsedNanos);
                _tickCount++;
                if (tickMillis > 0)
                {
                    //waitUntilNextTick 睡到目标时刻 落后时该值为负不睡直接进下一 tick
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

    //Tick 单帧逻辑基类空壳子类按需重写
    //原版 Tick 含世界推进/玩家调度/网络处理此处仅做骨架
    protected virtual void Tick()
    {
    }

    //EnqueueConsoleCommand 投递一条控制台命令 由下一次主循环取出执行
    //控制台线程与 GUI 线程都走这条 对应原版 handleConsoleInput 只入队
    public void EnqueueConsoleCommand(CommandSourceStack source, string command)
        => _consoleCommands.Enqueue((source, command, null));

    //ExecuteBlocking 在主循环线程执行一条命令并阻塞到执行完 对应原版 executeBlocking
    //RCON 这类要同步回包的调用方走它 命令与控制台命令同一条队列看到同一拍边界上的世界状态
    //主循环已退出时永远等不到结果 调用方线程靠 GenericThread.Stop 的打断脱身
    public void ExecuteBlocking(CommandSourceStack source, string command)
    {
        var done = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _consoleCommands.Enqueue((source, command, done));
        done.Task.Wait();
    }

    //DrainConsoleCommands 取出并执行控制台投递的命令
    //命令体自己抛的异常不该带走主循环 与控制台线程上的兜底一致
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

    //Stop 触发主循环退出由外部或 ShutdownHook 调用
    //子类重写时必须调 base.Stop 保证 _running 与 Cts 状态
    public virtual void Stop()
    {
        Log.Info("MinecraftServer received stop signal");
        _running = false;
        _shutdownCts.Cancel();
    }

    //WaitForShutdown 阻塞调用线程直到服务端关闭外部 EXE 用此保持进程
    public void WaitForShutdown()
    {
        try
        {
            _shutdownCts.Token.WaitHandle.WaitOne();
        }
        catch (OperationCanceledException)
        {
            //正常退出
        }
    }
}
