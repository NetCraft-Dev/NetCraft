using System.Threading;

namespace NetCraft.Util.Profiling;

//profiler静态门面对应原版net.minecraft.util.profiling.Profiler
//ThreadLocal管理活跃ProfilerFiller无Tracy时回退InactiveProfiler
public static class Profiler
{
    private static readonly ThreadLocal<ProfilerFiller?> Active = new();
    private static int _activeCount;

    //use作用域对应原版use返回Scope Dispose时stopUsing
    public static Scope Use(ProfilerFiller filler)
    {
        StartUsing(filler);
        return new Scope(StopUsing);
    }

    private static void StartUsing(ProfilerFiller filler)
    {
        if (Active.Value is not null) throw new InvalidOperationException("Profiler is already active");
        var decorated = ProfilerFiller.Combine(GetDefaultFiller(), filler);
        Active.Value = decorated;
        Interlocked.Increment(ref _activeCount);
        decorated.StartTick();
    }

    private static void StopUsing()
    {
        var active = Active.Value ?? throw new InvalidOperationException("Profiler was not active");
        Active.Value = null;
        Interlocked.Decrement(ref _activeCount);
        active.EndTick();
    }

    //获取当前filler对应原版get无活跃返回默认filler
    public static ProfilerFiller Get()
    {
        if (Volatile.Read(ref _activeCount) == 0) return GetDefaultFiller();
        return Active.Value ?? GetDefaultFiller();
    }

    //默认filler对应原版getDefaultFiller无Tracy返回InactiveProfiler
    private static ProfilerFiller GetDefaultFiller() => InactiveProfiler.Instance;

    //Scope对应原版Profiler.Scope AutoCloseable
    public readonly struct Scope : IDisposable
    {
        private readonly Action _onClose;

        internal Scope(Action onClose) => _onClose = onClose;

        public void Dispose() => _onClose();
    }
}
