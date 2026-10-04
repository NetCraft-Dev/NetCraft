namespace NetCraft.Primitives.Phys;

//CubeVoxelShape 等分网格形状 对应原版 CubeVoxelShape
//坐标是 0..1 的等分点 网格份数就是离散格数 是最紧凑的表示
public sealed class CubeVoxelShape : VoxelShape
{
    public CubeVoxelShape(DiscreteVoxelShape shape) : base(shape) { }

    public override IReadOnlyList<double> GetCoords(Direction.Axis axis) => new CubePointRange(Shape.GetSize(axis));

    //等分网格直接乘份数取整定位 不必二分 对应原版 findIndex
    protected override int FindIndex(Direction.Axis axis, double coord)
    {
        var size = Shape.GetSize(axis);
        return (int)Math.Floor(Math.Clamp(coord * size, -1.0, size));
    }
}
