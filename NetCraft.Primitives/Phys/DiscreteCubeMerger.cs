namespace NetCraft.Primitives.Phys;

//DiscreteCubeMerger merging of two discrete cube grids, maps to vanilla DiscreteCubeMerger
//Aligns the two grids by the least common multiple, uses gcd to simplify the index steps on both sides
public sealed class DiscreteCubeMerger : IIndexMerger
{
    private readonly CubePointRange _result;
    private readonly int _firstDiv;
    private readonly int _secondDiv;

    public DiscreteCubeMerger(int firstSize, int secondSize)
    {
        _result = new CubePointRange((int)Shapes.Lcm(firstSize, secondSize));
        var gcd = Shapes.Gcd(firstSize, secondSize);
        _firstDiv = firstSize / gcd;
        _secondDiv = secondSize / gcd;
    }

    public IReadOnlyList<double> List => _result;

    public int Size => _result.Count;

    public bool ForMergedIndexes(IIndexMerger.IndexConsumer consumer)
    {
        var size = _result.Count - 1;
        for (var i = 0; i < size; i++)
            if (!consumer(i / _secondDiv, i / _firstDiv, i)) return false;
        return true;
    }
}
