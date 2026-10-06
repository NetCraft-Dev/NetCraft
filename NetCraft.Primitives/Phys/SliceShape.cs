namespace NetCraft.Primitives.Phys;

//SliceShape slice shape, maps to vanilla SliceShape
//Takes one thin layer along an axis, face-occlusion tests only look at that layer, cheaper than computing the whole shape
public class SliceShape : VoxelShape
{
    //The slice is flattened to one cell thick, that axis has only the two coordinates 0 and 1
    private static readonly IReadOnlyList<double> SliceCoords = new CubePointRange(1);

    private readonly VoxelShape _source;
    private readonly Direction.Axis _axis;

    public SliceShape(VoxelShape source, Direction.Axis axis, int point)
        : base(MakeSlice(source.Shape, axis, point))
    {
        _source = source;
        _axis = axis;
    }

    //MakeSlice picks the point-th layer along the axis, the other two axes keep their original range, maps to vanilla makeSlice
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
