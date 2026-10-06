namespace NetCraft.Primitives.Phys;

//SubShape sub-region view, maps to vanilla SubShape
//Does not copy the bitmap, only records a rectangular region of the parent shape, reads and writes are forwarded to the parent
//Slice and clipped shapes use it to avoid copying the whole table
public sealed class SubShape : DiscreteVoxelShape
{
    private readonly DiscreteVoxelShape _parent;
    private readonly int _startX;
    private readonly int _startY;
    private readonly int _startZ;
    private readonly int _endX;
    private readonly int _endY;
    private readonly int _endZ;

    internal SubShape(DiscreteVoxelShape parent, int startX, int startY, int startZ, int endX, int endY, int endZ)
        : base(endX - startX, endY - startY, endZ - startZ)
    {
        _parent = parent;
        _startX = startX;
        _startY = startY;
        _startZ = startZ;
        _endX = endX;
        _endY = endY;
        _endZ = endZ;
    }

    public override bool IsFull(int x, int y, int z) => _parent.IsFull(_startX + x, _startY + y, _startZ + z);

    public override void Fill(int x, int y, int z) => _parent.Fill(_startX + x, _startY + y, _startZ + z);

    public override int FirstFull(Direction.Axis axis) => ClampToShape(axis, _parent.FirstFull(axis));

    public override int LastFull(Direction.Axis axis) => ClampToShape(axis, _parent.LastFull(axis));

    //The parent's result may fall outside this region, clamp it into the region then convert to sub-coordinates, maps to vanilla clampToShape
    private int ClampToShape(Direction.Axis axis, int parentResult)
    {
        var start = axis.Choose(_startX, _startY, _startZ);
        var end = axis.Choose(_endX, _endY, _endZ);
        return Math.Clamp(parentResult, start, end) - start;
    }
}
