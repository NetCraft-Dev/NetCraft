namespace NetCraft.Primitives.Phys;

//DiscreteVoxelShape 离散体素形状 对应原版 DiscreteVoxelShape
//把形状切成网格小立方格用位图存 布尔运算在格子上做 比逐盒判定快得多
//这里只记格子下标 实际长度单位由 VoxelShape 侧的坐标序列给出
public abstract class DiscreteVoxelShape
{
    private static readonly Direction.Axis[] AxisValues =
    {
        Direction.Axis.X,
        Direction.Axis.Y,
        Direction.Axis.Z,
    };

    //IntFaceConsumer 朝外的面回调 对应原版 DiscreteVoxelShape.IntFaceConsumer
    public delegate void IntFaceConsumer(Direction direction, int x, int y, int z);

    //IntLineConsumer 棱或盒的两角点回调 对应原版 DiscreteVoxelShape.IntLineConsumer
    public delegate void IntLineConsumer(int x1, int y1, int z1, int x2, int y2, int z2);

    protected DiscreteVoxelShape(int xSize, int ySize, int zSize)
    {
        if (xSize < 0 || ySize < 0 || zSize < 0)
            throw new ArgumentException($"尺寸必须非负 x:{xSize} y:{ySize} z:{zSize}");
        XSize = xSize;
        YSize = ySize;
        ZSize = zSize;
    }

    //三轴格子数
    public int XSize { get; }
    public int YSize { get; }
    public int ZSize { get; }

    public abstract bool IsFull(int x, int y, int z);

    public abstract void Fill(int x, int y, int z);

    public abstract int FirstFull(Direction.Axis axis);

    public abstract int LastFull(Direction.Axis axis);

    //Rotate 按八面体群整块旋转离散网格 对应原版 rotate
    //变换可能把某轴翻过来 负尺寸取绝对值 并用 -值-1 作为该轴位移把格子挪回正区间
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

    //IsFullWide 越界一律算空 对应原版 isFullWide
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

    //IsEmpty 任一轴上首尾重合即空 对应原版 isEmpty
    public virtual bool IsEmpty()
    {
        foreach (var axis in AxisValues)
            if (FirstFull(axis) >= LastFull(axis)) return true;
        return false;
    }

    public int GetSize(Direction.Axis axis) => axis.Choose(XSize, YSize, ZSize);

    //FirstFull 沿 aAxis 找出该行列上第一个实心格 找不到返回 aSize 对应原版 firstFull
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

    //LastFull 沿 aAxis 找出最后一个实心格 返回其下标加一 对应原版 lastFull
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

    //ForAllEdges 遍历所有外棱 三轴各扫一遍 对应原版 forAllEdges
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
                //看围绕这条棱的四个格子构成什么形状 一格或三格实心 或两格对角实心都算外棱
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

    //ForAllFaces 遍历所有朝外的面 对应原版 forAllFaces
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
                //实心段起点朝负向出面 终点朝正向出面
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
