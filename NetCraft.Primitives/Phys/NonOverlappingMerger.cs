namespace NetCraft.Primitives.Phys;

//NonOverlappingMerger 两段不重叠坐标的拼接归并 对应原版 NonOverlappingMerger
//一侧切分点整体小于另一侧时直接首尾相接 不必逐点比较
//swap 为真表示两侧在归并回调里的角色互换
public sealed class NonOverlappingMerger : AbstractDoubleList, IIndexMerger
{
    private readonly IReadOnlyList<double> _lower;
    private readonly IReadOnlyList<double> _upper;
    private readonly bool _swap;

    public NonOverlappingMerger(IReadOnlyList<double> lower, IReadOnlyList<double> upper, bool swap)
    {
        _lower = lower;
        _upper = upper;
        _swap = swap;
    }

    public override double this[int index]
        => index < _lower.Count ? _lower[index] : _upper[index - _lower.Count];

    public override int Count => _lower.Count + _upper.Count;

    public IReadOnlyList<double> List => this;

    public int Size => Count;

    public bool ForMergedIndexes(IIndexMerger.IndexConsumer consumer)
        => _swap
            ? ForNonSwappedIndexes((first, second, result) => consumer(second, first, result))
            : ForNonSwappedIndexes(consumer);

    private bool ForNonSwappedIndexes(IIndexMerger.IndexConsumer consumer)
    {
        var lowerSize = _lower.Count;
        for (var i = 0; i < lowerSize; i++)
            if (!consumer(i, -1, i)) return false;

        var upperSize = _upper.Count - 1;
        for (var i = 0; i < upperSize; i++)
            if (!consumer(lowerSize - 1, i, lowerSize + i)) return false;
        return true;
    }
}
