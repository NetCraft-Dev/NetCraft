namespace NetCraft.Primitives.Phys;

//OffsetDoubleList 整体平移的坐标序列 对应原版 OffsetDoubleList
//形状移动时坐标统一加偏移 不必重建底层数组
public sealed class OffsetDoubleList : AbstractDoubleList
{
    private readonly IReadOnlyList<double> _source;
    private readonly double _offset;

    public OffsetDoubleList(IReadOnlyList<double> source, double offset)
    {
        _source = source;
        _offset = offset;
    }

    public override double this[int index] => _source[index] + _offset;

    public override int Count => _source.Count;
}
