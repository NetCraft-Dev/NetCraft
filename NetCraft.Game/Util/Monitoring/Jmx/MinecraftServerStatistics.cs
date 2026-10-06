using System.Diagnostics.Metrics;
using NetCraft.Game.Server;

namespace NetCraft.Game.Util.Monitoring.Jmx;

//MinecraftServerStatistics server runtime metrics, maps to vanilla net.minecraft.util.monitoring.jmx.MinecraftServerStatistics
//Vanilla registers a JMX MBean exposing the tickTimes and averageTickTime attributes
//C# has no JMX, so System.Diagnostics.Metrics reports the same-named metrics; dotnet-counters can subscribe
public sealed class MinecraftServerStatistics : IDisposable
{
    //MeterName metric provider name, maps to the ObjectName domain of the vanilla MBean
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

    //Register registers runtime metrics for the server, maps to vanilla registerJmxMonitoring
    public static MinecraftServerStatistics Register(MinecraftServer server) => new(server);

    //ObserveAverageTickTime reports the average per-tick time of recent samples; nanoseconds converted to milliseconds
    private float ObserveAverageTickTime() => _server.AverageTickTimeNanos / 1_000_000f;

    //ObserveTickTimes reports recent per-tick times one by one; subscribers receive the latest ticks in time order
    private IEnumerable<Measurement<float>> ObserveTickTimes()
    {
        foreach (var nanos in _server.TickTimesNanos)
        {
            yield return new Measurement<float>(nanos / 1_000_000f);
        }
    }

    public void Dispose() => _meter.Dispose();
}
