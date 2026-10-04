using NetCraft.Util.Profiling.Metrics;

namespace NetCraft.Util.Profiling;

//profiler填充器接口对应原版net.minecraft.util.profiling.ProfilerFiller
//提供push/pop/incrementCounter/zone等基础探查操作
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

    //zone附加文本对应原版addZoneText默认空实现
    void AddZoneText(string text) { }

    //zone附加值对应原版addZoneValue默认空实现
    void AddZoneValue(long value) { }

    //zone颜色对应原版setZoneColor默认空实现
    void SetZoneColor(int color) { }

    //zone作用域对应原版zone(name)自动push返回Zone Dispose时pop
    Zone Zone(string name)
    {
        Push(name);
        return new Zone(this);
    }

    //zone作用域lazy版对应原版zone(Supplier)
    Zone Zone(Func<string> name)
    {
        Push(name);
        return new Zone(this);
    }

    //单次自增对应原版incrementCounter(name)
    void IncrementCounter(string name) => IncrementCounter(name, 1);

    //单次自增lazy版对应原版incrementCounter(Supplier)
    void IncrementCounter(Func<string> name) => IncrementCounter(name, 1);

    //合并两个filler对应原版ProfilerFiller.combine
    //InactiveProfiler实例跳过合并直接返回另一个
    static ProfilerFiller Combine(ProfilerFiller? first, ProfilerFiller? second)
    {
        if (ReferenceEquals(first, InactiveProfiler.Instance)) return second!;
        if (ReferenceEquals(second, InactiveProfiler.Instance)) return first!;
        if (first is null) return second!;
        if (second is null) return first;
        return new CombinedProfileFiller(first, second);
    }
}

//组合profiler对应原版ProfilerFiller.CombinedProfileFiller
//两个filler同步调用
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
