using NetCraft.Registry;

namespace NetCraft.Game.Server;

//Stopwatches 命名计时器集合 对应原版 net.minecraft.world.Stopwatches
///stopwatch 命令按 id 创建查询重启 用于给一段逻辑掐表
public sealed class Stopwatches
{
    //_entries id 到起始毫秒时刻 只存零时刻 耗时查询时现算
    private readonly Dictionary<Identifier, long> _entries = new();

    //CurrentTime 单调递增的毫秒时刻 对应原版 Stopwatches.currentTime
    public static long CurrentTime() => Environment.TickCount64;

    //Add 新建计时器 已存在返回 false
    public bool Add(Identifier id, long startMillis) => _entries.TryAdd(id, startMillis);

    //Restart 把已存在计时器重置到指定时刻 不存在返回 false
    public bool Restart(Identifier id, long startMillis)
    {
        if (!_entries.ContainsKey(id)) return false;
        _entries[id] = startMillis;
        return true;
    }

    //GetStart 取起始时刻 不存在返回 null
    public long? GetStart(Identifier id) => _entries.TryGetValue(id, out var start) ? start : null;

    //Remove 移除计时器 不存在返回 false
    public bool Remove(Identifier id) => _entries.Remove(id);
}
