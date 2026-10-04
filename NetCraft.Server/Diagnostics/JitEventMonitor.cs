using System.Diagnostics.Tracing;
using NetCraft.Logging;

namespace NetCraft.Server.Diagnostics;

//JitTier 一个方法目前被编译到的最高层级
public enum JitTier
{
    //Tier0 只做过首次快速编译
    Tier0,
    //Tier1 被重新优化过一次
    Tier1,
    //Tier1Plus Tier2 及以上 对优化结果再做优化
    Tier1Plus,
}

//JitMethodState 一个方法的编译状态
public sealed class JitMethodState
{
    public JitMethodState(ulong moduleId, uint token, string name, JitTier tier, long changedAt)
    {
        ModuleId = moduleId;
        Token = token;
        Name = name;
        Tier = tier;
        ChangedAt = changedAt;
    }

    //ModuleId 与 Token 合起来是方法身份 面板靠它把前后两次快照里的同一行对上
    public ulong ModuleId { get; }
    public uint Token { get; }
    //Name 方法全名
    public string Name { get; }
    //Tier 当前最高层级
    public JitTier Tier { get; internal set; }
    //ChangedAt 最近一次状态变化的时刻 同层里按它倒序就是"刚升级的浮到最前"
    public long ChangedAt { get; internal set; }
}

//JitEventMonitor 订阅本进程的 JIT 事件 供内存图打点与 JIT 列表展示
//走 EventListener 而不是 EventPipe 运行时事件源本来就归它管 不必再绕一圈解析
//层级按 MethodLoadVerbose 的 ReJITID 判: 0 是首次编译 1 是 Tier 1 再往上是对上一层的进一步优化
public static class JitEventMonitor
{
    //JitKeyword 与 NGenKeyword JIT 的方法加载事件按这两组发出来
    private const EventKeywords Keywords = (EventKeywords)(0x10 | 0x20);

    //WhitelistEnabled 是否只统计自己项目的编译事件 默认开
    //置 NETCRAFT_JIT_WHITELIST=0 可以放开成统计全部方法 排查第三方库的重编译时用
    private static readonly bool WhitelistEnabled =
        Environment.GetEnvironmentVariable("NETCRAFT_JIT_WHITELIST") is not ("0" or "false" or "False" or "FALSE");

    private static readonly Listener Hook = new();
    //Methods 方法编译状态表 键是模块与方法令牌
    //事件回调在监听线程上跑 快照从 UI 线程取 两边都要过这把锁
    private static readonly Dictionary<(ulong ModuleId, uint Token), JitMethodState> Methods = new();
    private static long _version;
    private static int _pendingTier1;
    private static int _pendingTier2;
    private static long _total;
    private static long _totalTier2;
    private static long _totalEvents;

    //Total 累计 Tier 1 重编译次数
    public static long Total => Interlocked.Read(ref _total);

    //TotalEvents 累计收到的方法加载事件数 用来确认订阅链路是通的
    public static long TotalEvents => Interlocked.Read(ref _totalEvents);

    //Version 状态表的变更版本 列表面板据此判断要不要重建 版本没变就不必重算排序
    public static long Version => Interlocked.Read(ref _version);

    //Start 触发订阅 静态字段初始化时就已经挂上 这里只是把入口补齐便于和 GC 那边对称
    public static void Start() => _ = Hook;

    //TakePending 取走两次取样之间累计的 Tier1 与 Tier2 及以上次数并清零
    public static (int Tier1, int Tier2) TakePending()
        => (Interlocked.Exchange(ref _pendingTier1, 0), Interlocked.Exchange(ref _pendingTier2, 0));

    //IsTracked 命名空间白名单 只统计自己项目的编译事件
    //运行时与第三方库的重编译量大又与 nc 无关 混进来会把基线上的点糊成一片
    //被 NETCRAFT_JIT_WHITELIST=0 关掉后一律放行
    public static bool IsTracked(string? methodNamespace)
        => !WhitelistEnabled
            || (methodNamespace is not null && methodNamespace.StartsWith("NetCraft", StringComparison.Ordinal));

    //Snapshot 取状态快照 已经排好序 可直接铺进列表
    public static List<JitMethodState> Snapshot()
    {
        lock (Methods)
        {
            var list = new List<JitMethodState>(Methods.Values);
            list.Sort(Compare);
            return list;
        }
    }

    //Compare 先按层级 T1+ > T1 > T0 再按最近变化时间倒序 最后用名字兜底保证顺序稳定
    private static int Compare(JitMethodState left, JitMethodState right)
    {
        var byTier = right.Tier.CompareTo(left.Tier);
        if (byTier != 0) return byTier;
        var byTime = right.ChangedAt.CompareTo(left.ChangedAt);
        if (byTime != 0) return byTime;
        return string.CompareOrdinal(left.Name, right.Name);
    }

    //Track 记下一个方法的编译层级 首次编译建条目 之后只往上抬
    private static void Track(ulong moduleId, uint token, string name, ulong reJitId)
    {
        //两个都取不到说明这条事件的模块信息没解析出来 记进去会把所有方法挤成一条
        if (moduleId == 0 && token == 0) return;
        var tier = reJitId switch
        {
            0 => JitTier.Tier0,
            1 => JitTier.Tier1,
            _ => JitTier.Tier1Plus,
        };
        var now = Environment.TickCount64;
        lock (Methods)
        {
            if (!Methods.TryGetValue((moduleId, token), out var state))
            {
                Methods[(moduleId, token)] = new JitMethodState(moduleId, token, name, tier, now);
                Interlocked.Increment(ref _version);
                return;
            }
            //同层重发不动 只有真的往上抬才算一次状态变化
            if (tier <= state.Tier) return;
            state.Tier = tier;
            state.ChangedAt = now;
            Interlocked.Increment(ref _version);
        }
    }

    //Listener 事件回调 状态全放在静态字段上
    //EventListener 的基类构造期间就会回调 OnEventSourceCreated 实例字段那时还没初始化
    private sealed class Listener : EventListener
    {
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(source, EventLevel.Verbose, Keywords);
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            //MethodLoadVerbose 表示这次编译已经出码 加载完成才带得出 ReJITID
            if (e.EventName is null || !e.EventName.StartsWith("MethodLoadVerbose", StringComparison.Ordinal))
                return;
            Interlocked.Increment(ref _totalEvents);
            var ns = Payload(e, "MethodNamespace") as string;
            if (!IsTracked(ns)) return;
            var reJitId = Payload(e, "ReJITID") is ulong value ? value : 0;
            var name = $"{ns}.{Payload(e, "MethodName")}";
            Track(Payload(e, "ModuleID") is ulong module ? module : 0UL,
                Payload(e, "MethodToken") is uint token ? token : 0U, name, reJitId);
            if (reJitId == 0) return;
            if (reJitId == 1)
            {
                Interlocked.Increment(ref _pendingTier1);
                var total = Interlocked.Increment(ref _total);
                Log.Debug($"[JIT] Tier1 {name} #{total}");
                return;
            }
            //Tier 2 及以上 对上一层优化结果的再优化 单独计数单独配色
            Interlocked.Increment(ref _pendingTier2);
            var totalTier2 = Interlocked.Increment(ref _totalTier2);
            Log.Debug($"[JIT] Tier{reJitId} recompiled {name} #{totalTier2}");
        }

        //Payload 按字段名取原始值 缺字段返回 null
        private static object? Payload(EventWrittenEventArgs e, string field)
        {
            var names = e.PayloadNames;
            var payload = e.Payload;
            if (names is null || payload is null) return null;
            for (var i = 0; i < names.Count && i < payload.Count; i++)
                if (names[i] == field) return payload[i];
            return null;
        }
    }
}
