namespace NetCraft.Primitives.Phys;

//BitSetDiscreteVoxelShape bitmap discrete voxel shape, maps to vanilla BitSetDiscreteVoxelShape
//Each of the x*y*z cells takes one bit, the bounding box bounds are stored separately to avoid full-table scans in isEmpty and firstFull
public sealed class BitSetDiscreteVoxelShape : DiscreteVoxelShape
{
    //Initial value of the bounding box upper bound, maps to vanilla ChunkSkyLightSources.NEGATIVE_INFINITY
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
        //The bounding box treats the shape as empty, the upper bound stays at 0 and the lower bound is pushed outside the size
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

    //WithFilledBounds builds a solid block directly from the given bounding box, maps to vanilla withFilledBounds
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

    //Bitmap linear index, maps to vanilla getIndex
    private int GetIndex(int x, int y, int z) => (x * YSize + y) * ZSize + z;

    private void SetBit(int index) => _storage[index >> 6] |= 1L << index;

    private void ClearRange(int from, int to)
    {
        for (var i = from; i < to; i++) _storage[i >> 6] &= ~(1L << i);
    }

    //NextClearBit the first clear bit from from, maps to BitSet.nextClearBit
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
        //Tighten the bounding box while filling cells
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

    //IsInterior an interior cell enclosed on all six faces, maps to vanilla isInterior, used for light occlusion tests
    public bool IsInterior(int x, int y, int z)
    {
        var inside = x > 0 && x < XSize - 1 && y > 0 && y < YSize - 1 && z > 0 && z < ZSize - 1;
        return inside && IsFull(x, y, z)
            && IsFull(x - 1, y, z) && IsFull(x + 1, y, z)
            && IsFull(x, y - 1, z) && IsFull(x, y + 1, z)
            && IsFull(x, y, z - 1) && IsFull(x, y, z + 1);
    }

    //Join combines two discrete shapes by a boolean operation, maps to vanilla join
    //Each of the three axes has a merger aligning both sides' split points, op decides cell by cell whether the result is solid
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

    //ForAllBoxes merges discrete cells into as few boxes as possible, maps to vanilla forAllBoxes
    //Processing cell by cell would split a solid block into n^3 boxes, here it merges into strips along Z, spreads them into faces along X, then stacks them into volumes along Y
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

    //IsZStripFull whether the segment along Z from startZ to endZ is fully solid, maps to vanilla isZStripFull
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
