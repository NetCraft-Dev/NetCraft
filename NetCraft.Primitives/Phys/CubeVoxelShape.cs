namespace NetCraft.Primitives.Phys;

//CubeVoxelShape evenly divided grid shape, maps to vanilla CubeVoxelShape
//Coordinates are the evenly divided points in 0..1, the division count is the discrete cell count, the most compact representation
public sealed class CubeVoxelShape : VoxelShape
{
    public CubeVoxelShape(DiscreteVoxelShape shape) : base(shape) { }

    public override IReadOnlyList<double> GetCoords(Direction.Axis axis) => new CubePointRange(Shape.GetSize(axis));

    //The evenly divided grid locates by multiplying the division count and flooring, no binary search needed, maps to vanilla findIndex
    protected override int FindIndex(Direction.Axis axis, double coord)
    {
        var size = Shape.GetSize(axis);
        return (int)Math.Floor(Math.Clamp(coord * size, -1.0, size));
    }
}
