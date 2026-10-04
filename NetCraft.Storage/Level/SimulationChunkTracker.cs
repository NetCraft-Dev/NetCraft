using NetCraft.Primitives;

namespace NetCraft.Storage;

//SimulationChunkTracker 模拟等级传播对应原版 net.minecraft.server.level.SimulationChunkTracker
//源是"参与模拟"的票等级 结果自己存一张表 不写回持有器
public sealed class SimulationChunkTracker : ChunkTracker
{
    //MaxLevel 不参与模拟的等级 对应原版 MAX_LEVEL
    public const int MaxLevel = ChunkLevel.FullChunkLevel;

    //_chunks 区块到模拟等级的映射 表里没有即不参与模拟
    private readonly Dictionary<long, int> _chunks = new();
    private readonly TicketStorage _ticketStorage;

    public SimulationChunkTracker(TicketStorage ticketStorage)
        : base(MaxLevel + 1, 16, 256)
    {
        _ticketStorage = ticketStorage;
        ticketStorage.SetSimulationChunkUpdatedListener((node, level, onlyDecreased) => Update(node, level, onlyDecreased));
    }

    protected override int GetLevelFromSource(long packedPos)
        => _ticketStorage.GetTicketLevelAt(packedPos, true);

    protected override int GetLevel(long packedPos) => _chunks.GetValueOrDefault(packedPos, MaxLevel);

    protected override void SetLevel(long packedPos, int level)
    {
        if (level >= MaxLevel) _chunks.Remove(packedPos);
        else _chunks[packedPos] = level;
    }

    //GetLevel 按区块坐标取模拟等级 对应原版 getLevel(ChunkPos)
    public int GetLevel(ChunkPos pos) => GetLevel(pos.Pack());

    //GetLevelAt 按打包坐标取模拟等级 对应原版 getLevel(long)
    //表里没有的区块返回 MaxLevel 即"不参与模拟" 调用方据此判实体与方块能不能 tick
    public int GetLevelAt(long packedPos) => GetLevel(packedPos);

    //RunAllUpdates 一直推进到收敛 对应原版 runAllUpdates
    public void RunAllUpdates() => RunUpdates(int.MaxValue);
}
