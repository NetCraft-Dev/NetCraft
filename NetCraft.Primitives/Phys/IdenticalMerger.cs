namespace NetCraft.Primitives.Phys;

//IdenticalMerger merge when both sides have identical coordinates, maps to vanilla IdenticalMerger
//Split points correspond one to one, skipping the merge computation
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
