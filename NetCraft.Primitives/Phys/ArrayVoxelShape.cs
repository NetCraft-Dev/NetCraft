namespace NetCraft.Primitives.Phys;

//ArrayVoxelShape 坐标数组形状 对应原版 ArrayVoxelShape
//三轴各自带一份切分点序列 坐标不落在等分点上时用它
//离散格与坐标序列是一一对应的 同一个形状可以有粗细不同的两种表示
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
            throw new ArgumentException("坐标序列长度必须与形状各轴格数一致");
        _xs = xs;
        _ys = ys;
        _zs = zs;
    }

    //Fit 裁到指定长度 不足补零 对应原版 Arrays.copyOf
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
