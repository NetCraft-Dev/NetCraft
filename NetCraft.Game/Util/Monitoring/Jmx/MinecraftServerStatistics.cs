using System.Diagnostics.Metrics;
using NetCraft.Game.Server;

namespace NetCraft.Game.Util.Monitoring.Jmx;

//MinecraftServerStatistics 服务端运行指标 对应原版 net.minecraft.util.monitoring.jmx.MinecraftServerStatistics
//原版注册成 JMX MBean 暴露 tickTimes 与 averageTickTime 两个属性
//C# 没有 JMX 改用 System.Diagnostics.Metrics 上报同名指标 dotnet-counters 即可订阅
public sealed class MinecraftServerStatistics : IDisposable
{
    //MeterName 指标提供方名 对应原版 MBean 的 ObjectName 域
    public const string MeterName = "net.minecraft.server";

    private readonly MinecraftServer _server;
    private readonly Meter _meter;

    private MinecraftServerStatistics(MinecraftServer server)
    {
        _server = server;
        _meter = new Meter(MeterName);
        _meter.CreateObservableGauge("minecraft.server.average_tick_time", ObserveAverageTickTime, "ms", "Current average tick time (ms)");
        _meter.CreateObservableGauge("minecraft.server.tick_times", ObserveTickTimes, "ms", "Historical tick times (ms)");
    }

    //Register 为服务器注册运行指标 对应原版 registerJmxMonitoring
    public static MinecraftServerStatistics Register(MinecraftServer server) => new(server);

    //ObserveAverageTickTime 上报最近样本的平均单拍耗时 纳秒换算成毫秒
    private float ObserveAverageTickTime() => _server.AverageTickTimeNanos / 1_000_000f;

    //ObserveTickTimes 逐条上报历史单拍耗时 订阅方按时间顺序拿到最近若干拍
    private IEnumerable<Measurement<float>> ObserveTickTimes()
    {
        foreach (var nanos in _server.TickTimesNanos)
        {
            yield return new Measurement<float>(nanos / 1_000_000f);
        }
    }

    public void Dispose() => _meter.Dispose();
}
