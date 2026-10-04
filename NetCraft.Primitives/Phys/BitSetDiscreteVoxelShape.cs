namespace NetCraft.Primitives.Phys;

//BitSetDiscreteVoxelShape 位图离散体素形状 对应原版 BitSetDiscreteVoxelShape
//x*y*z 个格子各占一位 另存包围盒上下界 省掉 isEmpty 与 firstFull 的整表扫描
public sealed class BitSetDiscreteVoxelShape : DiscreteVoxelShape
{
    //包围盒上界初值 对应原版 ChunkSkyLightSources.NEGATIVE_INFINITY
    private const int NegativeInfinity = int.MinValue;

    private readonly long[] _storage;
    private int _xMin;
    private int _yMin;
    private int _zMin;
    private int _xMax;
    private int _yMax;
    private int _zMax;

    public BitSetDiscreteVoxelShape(int xSize, int ySize, int zSize) : base(xSize, ySize, zSize)
    {
        _storage = new long[(xSize * ySize * zSize + 63) >> 6];
        //包围盒按空处理 上界留在 0 下界推到尺寸外
        _xMin = xSize;
        _yMin = ySize;
        _zMin = zSize;
    }

    public BitSetDiscreteVoxelShape(DiscreteVoxelShape source) : base(source.XSize, source.YSize, source.ZSize)
    {
        _storage = new long[(source.XSize * source.YSize * source.ZSize + 63) >> 6];
        if (source is BitSetDiscreteVoxelShape bitSet)
        {
            Array.Copy(bitSet._storage, _storage, _storage.Length);
        }
        else
        {
            for (var x = 0; x < XSize; x++)
            for (var y = 0; y < YSize; y++)
            for (var z = 0; z < ZSize; z++)
                if (source.IsFull(x, y, z)) SetBit(GetIndex(x, y, z));
        }

        _xMin = source.FirstFull(Direction.Axis.X);
        _yMin = source.FirstFull(Direction.Axis.Y);
        _zMin = source.FirstFull(Direction.Axis.Z);
        _xMax = source.LastFull(Direction.Axis.X);
        _yMax = source.LastFull(Direction.Axis.Y);
        _zMax = source.LastFull(Direction.Axis.Z);
    }

    //WithFilledBounds 直接按给定包围盒造实心块 对应原版 withFilledBounds
    public static BitSetDiscreteVoxelShape WithFilledBounds(int xSize, int ySize, int zSize,
        int xMin, int yMin, int zMin, int xMax, int yMax, int zMax)
    {
        var shape = new BitSetDiscreteVoxelShape(xSize, ySize, zSize)
        {
            _xMin = xMin,
            _yMin = yMin,
            _zMin = zMin,
            _xMax = xMax,
            _yMax = yMax,
            _zMax = zMax,
        };
        for (var x = xMin; x < xMax; x++)
        for (var y = yMin; y < yMax; y++)
        for (var z = zMin; z < zMax; z++)
            shape.SetBit(shape.GetIndex(x, y, z));
        return shape;
    }

    //位图线性下标 对应原版 getIndex
    private int GetIndex(int x, int y, int z) => (x * YSize + y) * ZSize + z;

    private void SetBit(int index) => _storage[index >> 6] |= 1L << index;

    private void ClearRange(int from, int to)
    {
        for (var i = from; i < to; i++) _storage[i >> 6] &= ~(1L << i);
    }

    //NextClearBit 从 from 起第一个空位 对应 BitSet.nextClearBit
    private int NextClearBit(int from)
    {
        var total = _storage.Length * 64;
        for (var i = from; i < total; i++)
            if ((_storage[i >> 6] & (1L << i)) == 0) return i;
        return total;
    }

    public override bool IsFull(int x, int y, int z) => (_storage[GetIndex(x, y, z) >> 6] & (1L << GetIndex(x, y, z))) != 0;

    public override void Fill(int x, int y, int z)
    {
        var index = GetIndex(x, y, z);
        _storage[index >> 6] |= 1L << index;
        //填格同时收紧包围盒
        _xMin = Math.Min(_xMin, x);
        _yMin = Math.Min(_yMin, y);
        _zMin = Math.Min(_zMin, z);
        _xMax = Math.Max(_xMax, x + 1);
        _yMax = Math.Max(_yMax, y + 1);
        _zMax = Math.Max(_zMax, z + 1);
    }

    public override bool IsEmpty()
    {
        foreach (var word in _storage)
            if (word != 0) return false;
        return true;
    }

    public override int FirstFull(Direction.Axis axis) => axis.Choose(_xMin, _yMin, _zMin);

    public override int LastFull(Direction.Axis axis) => axis.Choose(_xMax, _yMax, _zMax);

    //IsInterior 六面都被包住的内部格 对应原版 isInterior 光照遮挡判定用
    public bool IsInterior(int x, int y, int z)
    {
        var inside = x > 0 && x < XSize - 1 && y > 0 && y < YSize - 1 && z > 0 && z < ZSize - 1;
        return inside && IsFull(x, y, z)
            && IsFull(x - 1, y, z) && IsFull(x + 1, y, z)
            && IsFull(x, y - 1, z) && IsFull(x, y + 1, z)
            && IsFull(x, y, z - 1) && IsFull(x, y, z + 1);
    }

    //Join 两个离散形状按布尔运算合成 对应原版 join
    //三轴各有一份归并器把两侧的切分点对齐 逐格套 op 决定结果是否实心
    public static BitSetDiscreteVoxelShape Join(DiscreteVoxelShape first, DiscreteVoxelShape second,
        IIndexMerger xMerger, IIndexMerger yMerger, IIndexMerger zMerger, BooleanOp op)
    {
        var shape = new BitSetDiscreteVoxelShape(xMerger.Size - 1, yMerger.Size - 1, zMerger.Size - 1);
        var bounds = new[]
        {
            int.MaxValue, int.MaxValue, int.MaxValue,
            NegativeInfinity, NegativeInfinity, NegativeInfinity,
        };
        xMerger.ForMergedIndexes((x1, x2, xr) =>
        {
            var updatedSlice = false;
            yMerger.ForMergedIndexes((y1, y2, yr) =>
            {
                var updatedColumn = false;
                zMerger.ForMergedIndexes((z1, z2, zr) =>
                {
                    if (!op(first.IsFullWide(x1, y1, z1), second.IsFullWide(x2, y2, z2))) return true;
                    shape.SetBit(shape.GetIndex(xr, yr, zr));
                    bounds[2] = Math.Min(bounds[2], zr);
                    bounds[5] = Math.Max(bounds[5], zr);
                    updatedColumn = true;
                    return true;
                });
                if (updatedColumn)
                {
                    bounds[1] = Math.Min(bounds[1], yr);
                    bounds[4] = Math.Max(bounds[4], yr);
                    updatedSlice = true;
                }
                return true;
            });
            if (updatedSlice)
            {
                bounds[0] = Math.Min(bounds[0], xr);
                bounds[3] = Math.Max(bounds[3], xr);
            }
            return true;
        });
        shape._xMin = bounds[0];
        shape._yMin = bounds[1];
        shape._zMin = bounds[2];
        shape._xMax = bounds[3] + 1;
        shape._yMax = bounds[4] + 1;
        shape._zMax = bounds[5] + 1;
        return shape;
    }

    //ForAllBoxes 把离散格合并成尽量少的盒 对应原版 forAllBoxes
    //逐个格子处理会把一个实心块拆成 n^3 个盒 这里先沿 Z 合并成条 再沿 X 铺成面 再沿 Y 叠成体
    internal static void ForAllBoxes(DiscreteVoxelShape voxelShape,
        DiscreteVoxelShape.IntLineConsumer consumer, bool mergeNeighbors)
    {
        var shape = new BitSetDiscreteVoxelShape(voxelShape);
        for (var y = 0; y < shape.YSize; y++)
        for (var x = 0; x < shape.XSize; x++)
        {
            var lastStartZ = -1;
            for (var z = 0; z <= shape.ZSize; z++)
            {
                if (shape.IsFullWide(x, y, z))
                {
                    if (mergeNeighbors)
                    {
                        if (lastStartZ == -1) lastStartZ = z;
                    }
                    else
                    {
                        consumer(x, y, z, x + 1, y + 1, z + 1);
                    }
                }
                else if (lastStartZ != -1)
                {
                    var endX = x;
                    var endY = y;
                    shape.ClearZStrip(lastStartZ, z, x, y);
                    while (shape.IsZStripFull(lastStartZ, z, endX + 1, y))
                    {
                        shape.ClearZStrip(lastStartZ, z, endX + 1, y);
                        endX++;
                    }
                    while (shape.IsXzRectangleFull(x, endX + 1, lastStartZ, z, endY + 1))
                    {
                        for (var cx = x; cx <= endX; cx++) shape.ClearZStrip(lastStartZ, z, cx, endY + 1);
                        endY++;
                    }
                    consumer(x, y, lastStartZ, endX + 1, endY + 1, z);
                    lastStartZ = -1;
                }
            }
        }
    }

    //IsZStripFull 沿 Z 从 startZ 到 endZ 这一段是否全实心 对应原版 isZStripFull
    private bool IsZStripFull(int startZ, int endZ, int x, int y)
        => x < XSize && y < YSize && NextClearBit(GetIndex(x, y, startZ)) >= GetIndex(x, y, endZ);

    private bool IsXzRectangleFull(int startX, int endX, int startZ, int endZ, int y)
    {
        for (var x = startX; x < endX; x++)
            if (!IsZStripFull(startZ, endZ, x, y)) return false;
        return true;
    }

    private void ClearZStrip(int startZ, int endZ, int x, int y)
        => ClearRange(GetIndex(x, y, startZ), GetIndex(x, y, endZ));
}
