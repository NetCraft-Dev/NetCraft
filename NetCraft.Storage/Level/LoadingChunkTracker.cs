namespace NetCraft.Storage;

//LoadingChunkTracker 加载等级传播对应原版 net.minecraft.server.level.LoadingChunkTracker
//源是"参与加载"的票等级 收敛结果写回 DistanceManager 的持有器
public sealed class LoadingChunkTracker : ChunkTracker
{
    //MaxLevel 没有持有器时的等级 比原版 MAX_LEVEL 大一档 对应原版局部常量
    private const int MaxLevel = ChunkLevel.MaxLevel + 1;

    private readonly DistanceManager _distanceManager;
    private readonly TicketStorage _ticketStorage;

    public LoadingChunkTracker(DistanceManager distanceManager, TicketStorage ticketStorage)
        : base(MaxLevel + 1, 16, 256)
    {
        _distanceManager = distanceManager;
        _ticketStorage = ticketStorage;
        ticketStorage.SetLoadingChunkUpdatedListener((node, level, onlyDecreased) => Update(node, level, onlyDecreased));
    }

    protected override int GetLevelFromSource(long packedPos)
        => _ticketStorage.GetTicketLevelAt(packedPos, false);

    //GetLevel 有持有器就取它的等级 待删或不存在的当作不加载
    protected override int GetLevel(long packedPos)
    {
        if (!_distanceManager.IsChunkToRemove(packedPos)
            && _distanceManager.GetChunk(packedPos) is { } holder)
            return holder.TicketLevel;
        return MaxLevel;
    }

    protected override void SetLevel(long packedPos, int level)
    {
        var holder = _distanceManager.GetChunk(packedPos);
        var oldLevel = holder?.TicketLevel ?? MaxLevel;
        if (oldLevel == level) return;
        //只有传播之后仍在方块可 tick 档以内的区块才新建持有器
        //判据必须是传过来的 level(BFS 传播结果) 不能用原始票等级:
        //视距外那一圈本来就没有票 等级却靠邻格衰减到了 32 那正是"只加载不 tick"的弱加载带
        //拿原始票等级去拦会把这一带整个抹掉 只剩视距内一片同档次的区块
        //再往外(33 及以上)只是加载范围的空壳 加载票在视距内一律 31 靠它衰减出的外圈没有实际用途
        if (holder is null && level > ChunkLevel.BlockTickingLevel) return;
        if (_distanceManager.UpdateChunkScheduling(packedPos, level, holder, oldLevel) is { } updated)
            _distanceManager.ChunksToUpdateFutures.Add(updated);
    }

    //RunDistanceUpdates 最多推进 count 个节点 对应原版 runDistanceUpdates
    public int RunDistanceUpdates(int count) => RunUpdates(count);
}
