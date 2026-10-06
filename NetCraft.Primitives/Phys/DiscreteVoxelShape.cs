namespace NetCraft.Primitives.Phys;

//DiscreteVoxelShape discrete voxel shape, maps to vanilla DiscreteVoxelShape
//Slices the shape into a grid of small cubes stored as a bitmap, boolean operations run on cells and are much faster than per-box tests
//Only cell indices are recorded here, the actual length units come from the coordinate sequence on the VoxelShape side
public abstract class DiscreteVoxelShape
{
    private static readonly Direction.Axis[] AxisValues =
    {
        Direction.Axis.X,
        Direction.Axis.Y,
        Direction.Axis.Z,
    };

    //IntFaceConsumer outward face callback, maps to vanilla DiscreteVoxelShape.IntFaceConsumer
    public delegate void IntFaceConsumer(Direction direction, int x, int y, int z);

    //IntLineConsumer callback for the two corners of an edge or box, maps to vanilla DiscreteVoxelShape.IntLineConsumer
    public delegate void IntLineConsumer(int x1, int y1, int z1, int x2, int y2, int z2);

    protected DiscreteVoxelShape(int xSize, int ySize, int zSize)
    {
        if (xSize < 0 || ySize < 0 || zSize < 0)
            throw new ArgumentException($"Size must be non-negative x:{xSize} y:{ySize} z:{zSize}");
        XSize = xSize;
        YSize = ySize;
        ZSize = zSize;
    }

    //Cell counts along the three axes
    public int XSize { get; }
    public int YSize { get; }
    public int ZSize { get; }

    public abstract bool IsFull(int x, int y, int z);

    public abstract void Fill(int x, int y, int z);

    public abstract int FirstFull(Direction.Axis axis);

    public abstract int LastFull(Direction.Axis axis);

    //Rotate rotates the whole discrete grid by an octahedral group, maps to vanilla rotate
    //The transform may flip an axis, take the absolute value of a negative size and use -value-1 as the axis shift to move cells back into the positive range
    public DiscreteVoxelShape Rotate(OctahedralGroup rotation)
    {
        if (rotation == OctahedralGroup.Identity) return this;
        var size = rotation.Rotate(new Vec3i(XSize, YSize, ZSize));
        var sizeX = size.X;
        var sizeY = size.Y;
        var sizeZ = size.Z;
        var shiftX = FixupCoordinate(ref sizeX);
        var shiftY = FixupCoordinate(ref sizeY);
        var shiftZ = FixupCoordinate(ref sizeZ);
        var newShape = new BitSetDiscreteVoxelShape(sizeX, sizeY, sizeZ);
        for (var x = 0; x < XSize; x++)
        for (var y = 0; y < YSize; y++)
        for (var z = 0; z < ZSize; z++)
            if (IsFull(x, y, z))
            {
                var newPos = rotation.Rotate(new Vec3i(x, y, z));
                newShape.Fill(shiftX + newPos.X, shiftY + newPos.Y, shiftZ + newPos.Z);
            }
        return newShape;
    }

    private static int FixupCoordinate(ref int value)
    {
        if (value >= 0) return 0;
        value = -value;
        return value - 1;
    }

    //IsFullWide treats out-of-bounds as empty, maps to vanilla isFullWide
    public bool IsFullWide(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0 || x >= XSize || y >= YSize || z >= ZSize) return false;
        return IsFull(x, y, z);
    }

    public bool IsFullWide(AxisCycle transform, int x, int y, int z)
        => IsFullWide(
            transform.Cycle(x, y, z, Direction.Axis.X),
            transform.Cycle(x, y, z, Direction.Axis.Y),
            transform.Cycle(x, y, z, Direction.Axis.Z));

    public bool IsFull(AxisCycle transform, int x, int y, int z)
        => IsFull(
            transform.Cycle(x, y, z, Direction.Axis.X),
            transform.Cycle(x, y, z, Direction.Axis.Y),
            transform.Cycle(x, y, z, Direction.Axis.Z));

    //IsEmpty empty when the first and last full cells coincide on any axis, maps to vanilla isEmpty
    public virtual bool IsEmpty()
    {
        foreach (var axis in AxisValues)
            if (FirstFull(axis) >= LastFull(axis)) return true;
        return false;
    }

    public int GetSize(Direction.Axis axis) => axis.Choose(XSize, YSize, ZSize);

    //FirstFull finds the first solid cell along aAxis in that row/column, returns aSize if none, maps to vanilla firstFull
    public int FirstFull(Direction.Axis aAxis, int b, int c)
    {
        var aSize = GetSize(aAxis);
        if (b < 0 || c < 0) return aSize;
        var bAxis = AxisCycle.Forward.Cycle(aAxis);
        var cAxis = AxisCycle.Backward.Cycle(aAxis);
        if (b >= GetSize(bAxis) || c >= GetSize(cAxis)) return aSize;
        var transform = AxisCycle.Between(Direction.Axis.X, aAxis);
        for (var a = 0; a < aSize; a++)
            if (IsFull(transform, a, b, c)) return a;
        return aSize;
    }

    //LastFull finds the last solid cell along aAxis, returns its index plus one, maps to vanilla lastFull
    public int LastFull(Direction.Axis aAxis, int b, int c)
    {
        if (b < 0 || c < 0) return 0;
        var bAxis = AxisCycle.Forward.Cycle(aAxis);
        var cAxis = AxisCycle.Backward.Cycle(aAxis);
        if (b >= GetSize(bAxis) || c >= GetSize(cAxis)) return 0;
        var aSize = GetSize(aAxis);
        var transform = AxisCycle.Between(Direction.Axis.X, aAxis);
        for (var a = aSize - 1; a >= 0; a--)
            if (IsFull(transform, a, b, c)) return a + 1;
        return 0;
    }

    //ForAllEdges iterates every outer edge, scanning each of the three axes once, maps to vanilla forAllEdges
    public void ForAllEdges(IntLineConsumer consumer, bool mergeNeighbors)
    {
        ForAllAxisEdges(consumer, AxisCycle.None, mergeNeighbors);
        ForAllAxisEdges(consumer, AxisCycle.Forward, mergeNeighbors);
        ForAllAxisEdges(consumer, AxisCycle.Backward, mergeNeighbors);
    }

    private void ForAllAxisEdges(IntLineConsumer consumer, AxisCycle transform, bool mergeNeighbors)
    {
        var inverse = transform.Inverse;
        var aSize = GetSize(inverse.Cycle(Direction.Axis.X));
        var bSize = GetSize(inverse.Cycle(Direction.Axis.Y));
        var cSize = GetSize(inverse.Cycle(Direction.Axis.Z));
        for (var a = 0; a <= aSize; a++)
        for (var b = 0; b <= bSize; b++)
        {
            var lastStart = -1;
            for (var c = 0; c <= cSize; c++)
            {
                //Look at the four cells around this edge, one or three solid cells, or two diagonally solid cells, are all considered an outer edge
                var fullSectors = 0;
                var oddSectors = 0;
                for (var da = 0; da <= 1; da++)
                for (var db = 0; db <= 1; db++)
                    if (IsFullWide(inverse, a + da - 1, b + db - 1, c))
                    {
                        fullSectors++;
                        oddSectors ^= da ^ db;
                    }

                if (fullSectors == 1 || fullSectors == 3 || (fullSectors == 2 && (oddSectors & 1) == 0))
                {
                    if (mergeNeighbors)
                    {
                        if (lastStart == -1) lastStart = c;
                    }
                    else
                    {
                        consumer(
                            inverse.Cycle(a, b, c, Direction.Axis.X),
                            inverse.Cycle(a, b, c, Direction.Axis.Y),
                            inverse.Cycle(a, b, c, Direction.Axis.Z),
                            inverse.Cycle(a, b, c + 1, Direction.Axis.X),
                            inverse.Cycle(a, b, c + 1, Direction.Axis.Y),
                            inverse.Cycle(a, b, c + 1, Direction.Axis.Z));
                    }
                }
                else if (lastStart != -1)
                {
                    consumer(
                        inverse.Cycle(a, b, lastStart, Direction.Axis.X),
                        inverse.Cycle(a, b, lastStart, Direction.Axis.Y),
                        inverse.Cycle(a, b, lastStart, Direction.Axis.Z),
                        inverse.Cycle(a, b, c, Direction.Axis.X),
                        inverse.Cycle(a, b, c, Direction.Axis.Y),
                        inverse.Cycle(a, b, c, Direction.Axis.Z));
                    lastStart = -1;
                }
            }
        }
    }

    public void ForAllBoxes(IntLineConsumer consumer, bool mergeNeighbors)
        => BitSetDiscreteVoxelShape.ForAllBoxes(this, consumer, mergeNeighbors);

    //ForAllFaces iterates every outward face, maps to vanilla forAllFaces
    public void ForAllFaces(IntFaceConsumer consumer)
    {
        ForAllAxisFaces(consumer, AxisCycle.None);
        ForAllAxisFaces(consumer, AxisCycle.Forward);
        ForAllAxisFaces(consumer, AxisCycle.Backward);
    }

    private void ForAllAxisFaces(IntFaceConsumer consumer, AxisCycle transform)
    {
        var inverse = transform.Inverse;
        var cAxis = inverse.Cycle(Direction.Axis.Z);
        var aSize = GetSize(inverse.Cycle(Direction.Axis.X));
        var bSize = GetSize(inverse.Cycle(Direction.Axis.Y));
        var cSize = GetSize(cAxis);
        var negative = Direction.ByAxisDirection(cAxis, Direction.AxisDirection.Negative);
        var positive = Direction.ByAxisDirection(cAxis, Direction.AxisDirection.Positive);
        for (var a = 0; a < aSize; a++)
        for (var b = 0; b < bSize; b++)
        {
            var lastFull = false;
            for (var c = 0; c <= cSize; c++)
            {
                //The start of a solid run emits a face toward the negative direction, the end toward the positive direction
                var full = c != cSize && IsFull(inverse, a, b, c);
                if (!lastFull && full)
                    consumer(negative,
                        inverse.Cycle(a, b, c, Direction.Axis.X),
                        inverse.Cycle(a, b, c, Direction.Axis.Y),
                        inverse.Cycle(a, b, c, Direction.Axis.Z));
                if (lastFull && !full)
                    consumer(positive,
                        inverse.Cycle(a, b, c - 1, Direction.Axis.X),
                        inverse.Cycle(a, b, c - 1, Direction.Axis.Y),
                        inverse.Cycle(a, b, c - 1, Direction.Axis.Z));
                lastFull = full;
            }
        }
    }
}
