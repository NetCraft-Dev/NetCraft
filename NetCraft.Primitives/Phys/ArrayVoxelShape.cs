namespace NetCraft.Primitives.Phys;

//ArrayVoxelShape coordinate array shape, maps to vanilla ArrayVoxelShape
//Each of the three axes carries its own split point sequence, used when coordinates do not fall on even division points
//Discrete cells and the coordinate sequence correspond one to one, the same shape can have representations of different granularity
public class ArrayVoxelShape : VoxelShape
{
    private readonly IReadOnlyList<double> _xs;
    private readonly IReadOnlyList<double> _ys;
    private readonly IReadOnlyList<double> _zs;

    internal ArrayVoxelShape(DiscreteVoxelShape shape, double[] xs, double[] ys, double[] zs)
        : this(shape,
            (IReadOnlyList<double>)Fit(xs, shape.XSize + 1),
            (IReadOnlyList<double>)Fit(ys, shape.YSize + 1),
            (IReadOnlyList<double>)Fit(zs, shape.ZSize + 1)) { }

    internal ArrayVoxelShape(DiscreteVoxelShape shape,
        IReadOnlyList<double> xs, IReadOnlyList<double> ys, IReadOnlyList<double> zs) : base(shape)
    {
        if (xs.Count != shape.XSize + 1 || ys.Count != shape.YSize + 1 || zs.Count != shape.ZSize + 1)
            throw new ArgumentException("Coordinate sequence length must match the cell counts of the shape axes");
        _xs = xs;
        _ys = ys;
        _zs = zs;
    }

    //Fit trims to the given length, zero-fills the shortfall, maps to vanilla Arrays.copyOf
    private static double[] Fit(double[] source, int length)
    {
        var result = new double[length];
        Array.Copy(source, result, Math.Min(source.Length, length));
        return result;
    }

    public override IReadOnlyList<double> GetCoords(Direction.Axis axis) => axis switch
    {
        Direction.Axis.X => _xs,
        Direction.Axis.Y => _ys,
        _ => _zs,
    };
}
