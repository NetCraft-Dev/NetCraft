using System.Diagnostics.Tracing;
using NetCraft.Logging;

namespace NetCraft.Server.Diagnostics;

//JitEventMonitor 订阅本进程的 JIT 事件 供内存图打点
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
    private static int _pendingTier1;
    private static int _pendingTier2;
    private static long _total;
    private static long _totalTier2;
    private static long _totalEvents;

    //Total 累计 Tier 1 重编译次数
    public static long Total => Interlocked.Read(ref _total);

    //TotalEvents 累计收到的方法加载事件数 用来确认订阅链路是通的
    public static long TotalEvents => Interlocked.Read(ref _totalEvents);

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
