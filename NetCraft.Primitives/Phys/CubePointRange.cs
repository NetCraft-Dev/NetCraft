namespace NetCraft.Primitives.Phys;

//CubePointRange evenly divided point sequence, maps to vanilla CubePointRange
//Splits unit length into parts giving parts+1 split points, the coordinate source of the discrete cube grid
public sealed class CubePointRange : AbstractDoubleList
{
    private readonly int _parts;

    public CubePointRange(int parts)
    {
        if (parts <= 0) throw new ArgumentException("At least 1 part required", nameof(parts));
        _parts = parts;
    }

    //Must be a floating point division, the decompiled output dropped the double cast, integer division would make everything 0
    public override double this[int index] => (double)index / _parts;

    public override int Count => _parts + 1;
}
