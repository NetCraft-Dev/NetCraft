using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using NetCraft.Logging;

namespace NetCraft.Server.Diagnostics;

//GcEventMonitor 订阅本进程的运行时 GC 事件 供内存图在基线上打点
//走 EventPipe 的 Microsoft-Windows-DotNETRuntime provider 只开 GCKeyword
//CoreCLR 的 NativeRuntimeEventSource 在非 NativeAOT 下是空实现 EventListener 拿不到这类原生事件
public static class GcEventMonitor
{
    private const string ProviderName = "Microsoft-Windows-DotNETRuntime";
    //GCKeyword 运行时 GC 事件组
    private const long GcKeyword = 0x1;
    //GC 事件量很低 小缓冲足够 开大只是白占内存
    private const int CircularBufferMb = 16;

    private static readonly Lock Sync = new();
    private static EventPipeSession? _session;
    private static bool _started;
    //_pending 自上次取样以来的 GC 次数 GUI 每 500ms 取走一次
    private static int _pending;
    private static long _total;

    //Total 累计 GC 次数
    public static long Total => Interlocked.Read(ref _total);

    //Start 启动订阅 只生效一次
    //起不来就降级成"没有红点" 监听本身是调试功能不该把服务端带下去
    public static void Start()
    {
        lock (Sync)
        {
            if (_started) return;
            _started = true;
            try
            {
                var providers = new[]
                {
                    new EventPipeProvider(ProviderName, EventLevel.Informational, GcKeyword, null),
                };
                _session = new DiagnosticsClient(Environment.ProcessId)
                    .StartEventPipeSession(providers, requestRundown: false, circularBufferMB: CircularBufferMb);
                new Thread(Pump) { Name = "NetCraft-GcEvents", IsBackground = true }
                    .Start(_session.EventStream);
            }
            catch (Exception e)
            {
                Log.Warning($"GC event subscription failed, the memory graph baseline will have no red dot: {e.Message}");
            }
        }
    }

    //Stop 结束订阅 幂等
    public static void Stop()
    {
        lock (Sync)
        {
            if (!_started) return;
            _started = false;
            try { _session?.Stop(); }
            catch { }
            _session = null;
        }
    }

    //TakePending 取走两次取样之间累计的 GC 次数并清零
    public static int TakePending() => Interlocked.Exchange(ref _pending, 0);

    //Pump 后台读事件流 与调用方线程无关
    private static void Pump(object? state)
    {
        var source = new EventPipeEventSource((Stream)state!);
        source.Clr.GCStart += OnGcStart;
        try
        {
            //Process 一直读到 session 关闭 期间事件在回调里处理
            source.Process();
        }
        catch (Exception e)
        {
            Log.Warning($"GC event stream broken, the memory graph baseline stops updating: {e.Message}");
        }
    }

    //OnGcStart 一次 GC 开始
    //Depth 就是代际 0/1/2 分别是 Gen0/Gen1/Gen2
    private static void OnGcStart(GCStartTraceData data)
    {
        Interlocked.Increment(ref _pending);
        var total = Interlocked.Increment(ref _total);
        Log.Debug($"[GC] Gen{data.Depth} {data.Reason} #{total}");
    }
}
