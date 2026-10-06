using System.Threading;

namespace NetCraft.Util.Profiling;

//Profiler static facade, maps to vanilla net.minecraft.util.profiling.Profiler
//ThreadLocal manages the active ProfilerFiller, falls back to InactiveProfiler without Tracy
public static class Profiler
{
    private static readonly ThreadLocal<ProfilerFiller?> Active = new();
    private static int _activeCount;

    //use scope, maps to vanilla use returning a Scope; stopUsing on Dispose
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

    //Gets the current filler, maps to vanilla get; returns the default filler when inactive
    public static ProfilerFiller Get()
    {
        if (Volatile.Read(ref _activeCount) == 0) return GetDefaultFiller();
        return Active.Value ?? GetDefaultFiller();
    }

    //Default filler, maps to vanilla getDefaultFiller; returns InactiveProfiler without Tracy
    private static ProfilerFiller GetDefaultFiller() => InactiveProfiler.Instance;

    //Scope maps to vanilla Profiler.Scope AutoCloseable
    public readonly struct Scope : IDisposable
    {
        private readonly Action _onClose;

        internal Scope(Action onClose) => _onClose = onClose;

        public void Dispose() => _onClose();
    }
}
