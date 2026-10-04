namespace NetCraft.Primitives.Phys;

//SliceShape 切片形状 对应原版 SliceShape
//沿某一轴取一层薄片 贴面遮挡判定只看那一层 比拿整个形状算便宜
public class SliceShape : VoxelShape
{
    //切片被压成一格厚 该轴只剩 0 与 1 两个坐标
    private static readonly IReadOnlyList<double> SliceCoords = new CubePointRange(1);

    private readonly VoxelShape _source;
    private readonly Direction.Axis _axis;

    public SliceShape(VoxelShape source, Direction.Axis axis, int point)
        : base(MakeSlice(source.Shape, axis, point))
    {
        _source = source;
        _axis = axis;
    }

    //MakeSlice 按轴挑出第 point 层 其余两轴保持原范围 对应原版 makeSlice
    private static DiscreteVoxelShape MakeSlice(DiscreteVoxelShape source, Direction.Axis axis, int point)
        => new SubShape(source,
            axis.Choose(point, 0, 0),
            axis.Choose(0, point, 0),
            axis.Choose(0, 0, point),
            axis.Choose(point + 1, source.XSize, source.XSize),
            axis.Choose(source.YSize, point + 1, source.YSize),
            axis.Choose(source.ZSize, source.ZSize, point + 1));

    public override IReadOnlyList<double> GetCoords(Direction.Axis axis)
        => axis == _axis ? SliceCoords : _source.GetCoords(axis);
}
