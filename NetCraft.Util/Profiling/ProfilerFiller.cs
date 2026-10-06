using NetCraft.Util.Profiling.Metrics;

namespace NetCraft.Util.Profiling;

//Profiler filler interface, maps to vanilla net.minecraft.util.profiling.ProfilerFiller
//Provides basic instrumentation: push/pop/incrementCounter/zone
public interface ProfilerFiller
{
    public const string Root = "root";

    void StartTick();

    void EndTick();

    void Push(string name);

    void Push(Func<string> name);

    void Pop();

    void PopPush(string name);

    void PopPush(Func<string> name);

    void MarkForCharting(MetricCategory category);

    void IncrementCounter(string name, int amount);

    void IncrementCounter(Func<string> name, int amount);

    //zone added text, maps to vanilla addZoneText, empty by default
    void AddZoneText(string text) { }

    //zone added value, maps to vanilla addZoneValue, empty by default
    void AddZoneValue(long value) { }

    //zone color, maps to vanilla setZoneColor, empty by default
    void SetZoneColor(int color) { }

    //zone scope, maps to vanilla zone(name); auto-pushes and returns a Zone, pops on Dispose
    Zone Zone(string name)
    {
        Push(name);
        return new Zone(this);
    }

    //zone scope lazy version, maps to vanilla zone(Supplier)
    Zone Zone(Func<string> name)
    {
        Push(name);
        return new Zone(this);
    }

    //Single increment, maps to vanilla incrementCounter(name)
    void IncrementCounter(string name) => IncrementCounter(name, 1);

    //Single increment lazy version, maps to vanilla incrementCounter(Supplier)
    void IncrementCounter(Func<string> name) => IncrementCounter(name, 1);

    //Merges two fillers, maps to vanilla ProfilerFiller.combine
    //An InactiveProfiler instance skips merging and returns the other directly
    static ProfilerFiller Combine(ProfilerFiller? first, ProfilerFiller? second)
    {
        if (ReferenceEquals(first, InactiveProfiler.Instance)) return second!;
        if (ReferenceEquals(second, InactiveProfiler.Instance)) return first!;
        if (first is null) return second!;
        if (second is null) return first;
        return new CombinedProfileFiller(first, second);
    }
}

//Combined profiler, maps to vanilla ProfilerFiller.CombinedProfileFiller
//Calls both fillers in sync
public sealed class CombinedProfileFiller : ProfilerFiller
{
    private readonly ProfilerFiller _first;
    private readonly ProfilerFiller _second;

    public CombinedProfileFiller(ProfilerFiller first, ProfilerFiller second)
    {
        _first = first;
        _second = second;
    }

    public void StartTick() { _first.StartTick(); _second.StartTick(); }
    public void EndTick() { _first.EndTick(); _second.EndTick(); }
    public void Push(string name) { _first.Push(name); _second.Push(name); }
    public void Push(Func<string> name) { _first.Push(name); _second.Push(name); }
    public void Pop() { _first.Pop(); _second.Pop(); }
    public void PopPush(string name) { _first.PopPush(name); _second.PopPush(name); }
    public void PopPush(Func<string> name) { _first.PopPush(name); _second.PopPush(name); }
    public void MarkForCharting(MetricCategory category) { _first.MarkForCharting(category); _second.MarkForCharting(category); }
    public void IncrementCounter(string name, int amount) { _first.IncrementCounter(name, amount); _second.IncrementCounter(name, amount); }
    public void IncrementCounter(Func<string> name, int amount) { _first.IncrementCounter(name, amount); _second.IncrementCounter(name, amount); }
    public void AddZoneText(string text) { _first.AddZoneText(text); _second.AddZoneText(text); }
    public void AddZoneValue(long value) { _first.AddZoneValue(value); _second.AddZoneValue(value); }
    public void SetZoneColor(int color) { _first.SetZoneColor(color); _second.SetZoneColor(color); }
}
