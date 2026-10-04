namespace NetCraft.Primitives.Phys;

//IdenticalMerger 两侧坐标完全一致的归并 对应原版 IdenticalMerger
//切分点一对一 省掉归并计算
public sealed class IdenticalMerger : IIndexMerger
{
    private readonly IReadOnlyList<double> _coords;

    public IdenticalMerger(IReadOnlyList<double> coords) => _coords = coords;

    public IReadOnlyList<double> List => _coords;

    public int Size => _coords.Count;

    public bool ForMergedIndexes(IIndexMerger.IndexConsumer consumer)
    {
        var size = _coords.Count - 1;
        for (var i = 0; i < size; i++)
            if (!consumer(i, i, i)) return false;
        return true;
    }
}
