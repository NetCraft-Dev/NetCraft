namespace NetCraft.Primitives.Phys;

//OffsetDoubleList coordinate sequence translated as a whole, maps to vanilla OffsetDoubleList
//When a shape moves, coordinates get a uniform offset added, no need to rebuild the underlying array
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
