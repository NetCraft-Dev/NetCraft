namespace NetCraft.Primitives.Phys;

//SubShape 子区域视图 对应原版 SubShape
//不复制位图 只记父形状里的一块矩形区域 读写都转发给父形状
//切面与裁剪形状靠它避免整表拷贝
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

    //父形状的结果可能落在这块区域外 夹到区域内再换算成子坐标 对应原版 clampToShape
    private int ClampToShape(Direction.Axis axis, int parentResult)
    {
        var start = axis.Choose(_startX, _startY, _startZ);
        var end = axis.Choose(_endX, _endY, _endZ);
        return Math.Clamp(parentResult, start, end) - start;
    }
}
