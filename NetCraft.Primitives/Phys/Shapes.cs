namespace NetCraft.Primitives.Phys;

//Shapes 形状工厂与布尔运算 对应原版 Shapes
//提供盒/整块/空三个基础形状 以及按布尔运算合成新形状的入口
//旋转镜像相关方法依赖 OctahedralGroup 留到 V-4 补齐
public static partial class Shapes
{
    public const double Epsilon = 1.0E-7;
    public const double BigEpsilon = 1.0E-6;

    //DoubleLineConsumer 盒或棱的两角点回调 对应原版 Shapes.DoubleLineConsumer
    public delegate void DoubleLineConsumer(double x1, double y1, double z1, double x2, double y2, double z2);

    private static readonly VoxelShape BlockShape = MakeBlock();

    //BlockCenter 方块中心 作为默认旋转中心
    private static readonly Vec3 BlockCenter = new(0.5, 0.5, 0.5);

    private static readonly VoxelShape EmptyShape = new ArrayVoxelShape(
        new BitSetDiscreteVoxelShape(0, 0, 0),
        new[] { 0.0 },
        new[] { 0.0 },
        new[] { 0.0 });

    public static readonly VoxelShape Infinity = Box(
        double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity,
        double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);

    private static readonly Direction.Axis[] AxisValues =
    {
        Direction.Axis.X,
        Direction.Axis.Y,
        Direction.Axis.Z,
    };

    private static VoxelShape MakeBlock()
    {
        var shape = new BitSetDiscreteVoxelShape(1, 1, 1);
        shape.Fill(0, 0, 0);
        return new CubeVoxelShape(shape);
    }

    public static VoxelShape Empty() => EmptyShape;

    public static VoxelShape Block() => BlockShape;

    public static VoxelShape Box(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        if (minX > maxX || minY > maxY || minZ > maxZ)
            throw new ArgumentException("最小值不能大于最大值");
        return Create(minX, minY, minZ, maxX, maxY, maxZ);
    }

    //Create 按能否落在 1/2/4/8 分格上选紧凑表示 对应原版 create
    //能整除就用离散立方网格 不能就用坐标数组 两者对外行为一致
    public static VoxelShape Create(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        if (maxX - minX < Epsilon || maxY - minY < Epsilon || maxZ - minZ < Epsilon) return Empty();

        var xBits = FindBits(minX, maxX);
        var yBits = FindBits(minY, maxY);
        var zBits = FindBits(minZ, maxZ);
        if (xBits < 0 || yBits < 0 || zBits < 0)
            return new ArrayVoxelShape(BlockShape.Shape,
                new[] { minX, maxX }, new[] { minY, maxY }, new[] { minZ, maxZ });
        if (xBits == 0 && yBits == 0 && zBits == 0) return Block();

        var xSize = 1 << xBits;
        var ySize = 1 << yBits;
        var zSize = 1 << zBits;
        var voxelShape = BitSetDiscreteVoxelShape.WithFilledBounds(xSize, ySize, zSize,
            (int)Math.Round(minX * xSize), (int)Math.Round(minY * ySize), (int)Math.Round(minZ * zSize),
            (int)Math.Round(maxX * xSize), (int)Math.Round(maxY * ySize), (int)Math.Round(maxZ * zSize));
        return new CubeVoxelShape(voxelShape);
    }

    public static VoxelShape Create(AABB aabb)
        => Create(aabb.Min.X, aabb.Min.Y, aabb.Min.Z, aabb.Max.X, aabb.Max.Y, aabb.Max.Z);

    //FindBits 取能整除这个坐标所需的最小分格位数 超出单位立方返回 -1 对应原版 findBits
    private static int FindBits(double min, double max)
    {
        if (min < -Epsilon || max > 1.0 + Epsilon) return -1;
        for (var bits = 0; bits <= 3; bits++)
        {
            var intervals = 1 << bits;
            var scaledMin = min * intervals;
            var scaledMax = max * intervals;
            var foundMin = Math.Abs(scaledMin - Math.Round(scaledMin)) < Epsilon * intervals;
            var foundMax = Math.Abs(scaledMax - Math.Round(scaledMax)) < Epsilon * intervals;
            if (foundMin && foundMax) return bits;
        }
        return -1;
    }

    //Lcm 最小公倍数 对应原版 Shapes.lcm 返回 long 因为两份网格相乘可能溢出 int
    internal static long Lcm(int first, int second) => (long)first * (second / Gcd(first, second));

    //Gcd 最大公约数 对应 Guava IntMath.gcd
    public static int Gcd(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }

    public static VoxelShape Or(VoxelShape first, VoxelShape second) => Join(first, second, BooleanOps.Or);

    public static VoxelShape Or(VoxelShape first, params VoxelShape[] tail)
    {
        var result = first;
        foreach (var shape in tail) result = Or(result, shape);
        return result;
    }

    //Join 布尔运算后顺手做一次盒合并 对应原版 join
    public static VoxelShape Join(VoxelShape first, VoxelShape second, BooleanOp op)
        => JoinUnoptimized(first, second, op).Optimize();

    //JoinUnoptimized 两侧形状按布尔运算合成为一个 对应原版 joinUnoptimized
    //先按各自坐标轴挑归并器 再在离散网格上逐格套 op
    public static VoxelShape JoinUnoptimized(VoxelShape first, VoxelShape second, BooleanOp op)
    {
        //两侧都空却要出实心的运算没有意义 直接挡掉避免后面下标越界
        if (op(false, false)) throw new ArgumentException("该布尔运算在两侧都空时会产出实心");
        if (ReferenceEquals(first, second)) return op(true, true) ? first : Empty();

        var firstOnlyMatters = op(true, false);
        var secondOnlyMatters = op(false, true);
        if (first.IsEmpty) return secondOnlyMatters ? second : Empty();
        if (second.IsEmpty) return firstOnlyMatters ? first : Empty();

        var xMerger = CreateIndexMerger(1, first.GetCoords(Direction.Axis.X), second.GetCoords(Direction.Axis.X),
            firstOnlyMatters, secondOnlyMatters);
        var yMerger = CreateIndexMerger(xMerger.Size - 1, first.GetCoords(Direction.Axis.Y), second.GetCoords(Direction.Axis.Y),
            firstOnlyMatters, secondOnlyMatters);
        var zMerger = CreateIndexMerger((xMerger.Size - 1) * (yMerger.Size - 1), first.GetCoords(Direction.Axis.Z),
            second.GetCoords(Direction.Axis.Z), firstOnlyMatters, secondOnlyMatters);

        var voxelShape = BitSetDiscreteVoxelShape.Join(first.Shape, second.Shape, xMerger, yMerger, zMerger, op);
        //三轴都是等分网格才能用离散表示 否则坐标不均匀只能退回坐标数组
        if (xMerger is DiscreteCubeMerger && yMerger is DiscreteCubeMerger && zMerger is DiscreteCubeMerger)
            return new CubeVoxelShape(voxelShape);
        return new ArrayVoxelShape(voxelShape, xMerger.List, yMerger.List, zMerger.List);
    }

    //JoinIsNotEmpty 只判布尔运算结果是否非空 不算出形状 对应原版 joinIsNotEmpty
    //比 Join 便宜得多 光照遮挡与碰撞预判大量用到
    public static bool JoinIsNotEmpty(VoxelShape first, VoxelShape second, BooleanOp op)
    {
        if (op(false, false)) throw new ArgumentException("该布尔运算在两侧都空时会产出实心");

        var firstEmpty = first.IsEmpty;
        var secondEmpty = second.IsEmpty;
        if (firstEmpty || secondEmpty) return op(!firstEmpty, !secondEmpty);
        if (ReferenceEquals(first, second)) return op(true, true);

        var firstOnlyMatters = op(true, false);
        var secondOnlyMatters = op(false, true);
        //任一轴上完全分离 结果就是两侧各自的取舍
        foreach (var axis in AxisValues)
        {
            if (first.Max(axis) < second.Min(axis) - Epsilon) return firstOnlyMatters || secondOnlyMatters;
            if (second.Max(axis) < first.Min(axis) - Epsilon) return firstOnlyMatters || secondOnlyMatters;
        }

        var xMerger = CreateIndexMerger(1, first.GetCoords(Direction.Axis.X), second.GetCoords(Direction.Axis.X),
            firstOnlyMatters, secondOnlyMatters);
        var yMerger = CreateIndexMerger(xMerger.Size - 1, first.GetCoords(Direction.Axis.Y), second.GetCoords(Direction.Axis.Y),
            firstOnlyMatters, secondOnlyMatters);
        var zMerger = CreateIndexMerger((xMerger.Size - 1) * (yMerger.Size - 1), first.GetCoords(Direction.Axis.Z),
            second.GetCoords(Direction.Axis.Z), firstOnlyMatters, secondOnlyMatters);
        return JoinIsNotEmpty(xMerger, yMerger, zMerger, first.Shape, second.Shape, op);
    }

    private static bool JoinIsNotEmpty(IIndexMerger xMerger, IIndexMerger yMerger, IIndexMerger zMerger,
        DiscreteVoxelShape first, DiscreteVoxelShape second, BooleanOp op)
        => !xMerger.ForMergedIndexes((x1, x2, _) =>
            yMerger.ForMergedIndexes((y1, y2, _) =>
                zMerger.ForMergedIndexes((z1, z2, _) =>
                    !op(first.IsFullWide(x1, y1, z1), second.IsFullWide(x2, y2, z2)))));

    //Collide 让一个移动盒沿轴推进 返回被这组形状挡住后的实际位移 对应原版 Shapes.collide
    public static double Collide(Direction.Axis axis, AABB moving, IEnumerable<VoxelShape> shapes, double distance)
    {
        foreach (var shape in shapes)
        {
            if (Math.Abs(distance) < Epsilon) return 0.0;
            distance = shape.Collide(axis, moving, distance);
        }
        return distance;
    }

    //BlockOccludes 判定一个形状的某面是否把方向的整面盖住 对应原版 blockOccludes
    public static bool BlockOccludes(VoxelShape shape, VoxelShape occluder, Direction direction)
    {
        if (ReferenceEquals(shape, Block()) && ReferenceEquals(occluder, Block())) return true;
        if (occluder.IsEmpty) return false;
        var axis = direction.GetAxis();
        var sign = direction.AxisDir;
        var first = sign == Direction.AxisDirection.Positive ? shape : occluder;
        var second = sign == Direction.AxisDirection.Positive ? occluder : shape;
        var op = sign == Direction.AxisDirection.Positive ? BooleanOps.OnlyFirst : BooleanOps.OnlySecond;
        return FuzzyEquals(first.Max(axis), 1.0)
            && FuzzyEquals(second.Min(axis), 0.0)
            && !JoinIsNotEmpty(new SliceShape(first, axis, first.Shape.GetSize(axis) - 1),
                new SliceShape(second, axis, 0), op);
    }

    //MergedFaceOccludes 合并面上是否无缝隙 对应原版 mergedFaceOccludes
    public static bool MergedFaceOccludes(VoxelShape shape, VoxelShape occluder, Direction direction)
    {
        if (ReferenceEquals(shape, Block()) || ReferenceEquals(occluder, Block())) return true;
        var axis = direction.GetAxis();
        var sign = direction.AxisDir;
        var first = sign == Direction.AxisDirection.Positive ? shape : occluder;
        var second = sign == Direction.AxisDirection.Positive ? occluder : shape;
        if (!FuzzyEquals(first.Max(axis), 1.0)) first = Empty();
        if (!FuzzyEquals(second.Min(axis), 0.0)) second = Empty();
        return !JoinIsNotEmpty(Block(),
            JoinUnoptimized(new SliceShape(first, axis, first.Shape.GetSize(axis) - 1),
                new SliceShape(second, axis, 0), BooleanOps.Or),
            BooleanOps.OnlyFirst);
    }

    public static bool FaceShapeOccludes(VoxelShape shape, VoxelShape occluder)
    {
        if (ReferenceEquals(shape, Block()) || ReferenceEquals(occluder, Block())) return true;
        if (shape.IsEmpty && occluder.IsEmpty) return false;
        return !JoinIsNotEmpty(Block(), JoinUnoptimized(shape, occluder, BooleanOps.Or), BooleanOps.OnlyFirst);
    }

    public static bool Equal(VoxelShape first, VoxelShape second)
        => !JoinIsNotEmpty(first, second, BooleanOps.NotSame);

    public static VoxelShape Rotate(VoxelShape shape, OctahedralGroup rotation)
        => Rotate(shape, rotation, BlockCenter);

    //Rotate 按八面体群旋转形状 对应原版 Shapes.rotate
    //离散格点转完后坐标轴可能互换 所以三轴坐标序列要按置换后的轴重取
    public static VoxelShape Rotate(VoxelShape shape, OctahedralGroup rotation, Vec3 rotationPoint)
    {
        if (rotation == OctahedralGroup.Identity) return shape;
        var newDiscreteShape = shape.Shape.Rotate(rotation);
        if (shape is CubeVoxelShape && BlockCenter == rotationPoint) return new CubeVoxelShape(newDiscreteShape);

        var newX = rotation.Permutation().PermuteAxis(Direction.Axis.X);
        var newY = rotation.Permutation().PermuteAxis(Direction.Axis.Y);
        var newZ = rotation.Permutation().PermuteAxis(Direction.Axis.Z);
        return new ArrayVoxelShape(newDiscreteShape,
            FlipAxisIfNeeded(shape.GetCoords(newX), rotation.Inverts(Direction.Axis.X),
                newX.Choose(rotationPoint.X, rotationPoint.Y, rotationPoint.Z), rotationPoint.X),
            FlipAxisIfNeeded(shape.GetCoords(newY), rotation.Inverts(Direction.Axis.Y),
                newY.Choose(rotationPoint.X, rotationPoint.Y, rotationPoint.Z), rotationPoint.Y),
            FlipAxisIfNeeded(shape.GetCoords(newZ), rotation.Inverts(Direction.Axis.Z),
                newZ.Choose(rotationPoint.X, rotationPoint.Y, rotationPoint.Z), rotationPoint.Z));
    }

    //FlipAxisIfNeeded 该轴取负时把坐标序列镜像 对应原版 flipAxisIfNeeded
    //旋转中心与旧中心在该轴上的投影相同时只是平移 不必重建序列
    internal static IReadOnlyList<double> FlipAxisIfNeeded(IReadOnlyList<double> newAxis, bool flip,
        double newRelative, double oldRelative)
    {
        if (!flip && newRelative == oldRelative) return newAxis;
        var size = newAxis.Count;
        var result = new double[size];
        if (flip)
            for (var i = size - 1; i >= 0; i--)
                result[size - 1 - i] = -(newAxis[i] - newRelative) + oldRelative;
        else
            for (var i = 0; i < size; i++)
                result[i] = (newAxis[i] - newRelative) + oldRelative;
        return result;
    }

    public static Dictionary<Direction.Axis, VoxelShape> RotateHorizontalAxis(VoxelShape zAxis)
        => RotateHorizontalAxis(zAxis, BlockCenter);

    //RotateHorizontalAxis 沿Z轴的形状转出沿X轴的 对应原版 rotateHorizontalAxis
    public static Dictionary<Direction.Axis, VoxelShape> RotateHorizontalAxis(VoxelShape zAxis, Vec3 rotationCenter)
        => new()
        {
            [Direction.Axis.Z] = zAxis,
            [Direction.Axis.X] = Rotate(zAxis, OctahedralGroups.BlockRotY90, rotationCenter),
        };

    public static Dictionary<Direction.Axis, VoxelShape> RotateAllAxis(VoxelShape north)
        => RotateAllAxis(north, BlockCenter);

    public static Dictionary<Direction.Axis, VoxelShape> RotateAllAxis(VoxelShape north, Vec3 rotationCenter)
        => new()
        {
            [Direction.Axis.Z] = north,
            [Direction.Axis.X] = Rotate(north, OctahedralGroups.BlockRotY90, rotationCenter),
            [Direction.Axis.Y] = Rotate(north, OctahedralGroups.BlockRotX90, rotationCenter),
        };

    public static Dictionary<Direction, VoxelShape> RotateHorizontal(VoxelShape north)
        => RotateHorizontal(north, OctahedralGroup.Identity, BlockCenter);

    public static Dictionary<Direction, VoxelShape> RotateHorizontal(VoxelShape north, OctahedralGroup initial)
        => RotateHorizontal(north, initial, BlockCenter);

    //RotateHorizontal 由北向形状转出水平四向 对应原版 rotateHorizontal
    //NORTH 那一格原版沿用默认旋转中心 这里照抄不改成 rotationCenter
    public static Dictionary<Direction, VoxelShape> RotateHorizontal(VoxelShape north, OctahedralGroup initial,
        Vec3 rotationCenter)
        => new()
        {
            [Direction.North] = Rotate(north, initial),
            [Direction.East] = Rotate(north, OctahedralGroups.BlockRotY90.Compose(initial), rotationCenter),
            [Direction.South] = Rotate(north, OctahedralGroups.BlockRotY180.Compose(initial), rotationCenter),
            [Direction.West] = Rotate(north, OctahedralGroups.BlockRotY270.Compose(initial), rotationCenter),
        };

    public static Dictionary<Direction, VoxelShape> RotateAll(VoxelShape north)
        => RotateAll(north, OctahedralGroup.Identity, BlockCenter);

    public static Dictionary<Direction, VoxelShape> RotateAll(VoxelShape north, Vec3 rotationCenter)
        => RotateAll(north, OctahedralGroup.Identity, rotationCenter);

    //RotateAll 由北向形状转出六向 对应原版 rotateAll
    public static Dictionary<Direction, VoxelShape> RotateAll(VoxelShape north, OctahedralGroup initial,
        Vec3 rotationCenter)
        => new()
        {
            [Direction.North] = Rotate(north, initial),
            [Direction.East] = Rotate(north, OctahedralGroups.BlockRotY90.Compose(initial), rotationCenter),
            [Direction.South] = Rotate(north, OctahedralGroups.BlockRotY180.Compose(initial), rotationCenter),
            [Direction.West] = Rotate(north, OctahedralGroups.BlockRotY270.Compose(initial), rotationCenter),
            [Direction.Up] = Rotate(north, OctahedralGroups.BlockRotX270.Compose(initial), rotationCenter),
            [Direction.Down] = Rotate(north, OctahedralGroups.BlockRotX90.Compose(initial), rotationCenter),
        };

    //CreateIndexMerger 按两侧坐标的形态挑最省的归并器 对应原版 createIndexMerger
    //cost 是当前轴上已经确定的格数 用它估归并结果规模 太小就不值得走离散对齐
    private static IIndexMerger CreateIndexMerger(int cost, IReadOnlyList<double> first, IReadOnlyList<double> second,
        bool firstOnlyMatters, bool secondOnlyMatters)
    {
        var firstSize = first.Count - 1;
        var secondSize = second.Count - 1;
        if (first is CubePointRange && second is CubePointRange)
        {
            var size = Lcm(firstSize, secondSize);
            if (cost * size <= 256) return new DiscreteCubeMerger(firstSize, secondSize);
        }

        if (first[firstSize] < second[0] - Epsilon) return new NonOverlappingMerger(first, second, false);
        if (second[secondSize] < first[0] - Epsilon) return new NonOverlappingMerger(second, first, true);
        if (firstSize == secondSize && first.SequenceEqual(second)) return new IdenticalMerger(first);
        return new IndirectMerger(first, second, firstOnlyMatters, secondOnlyMatters);
    }

    //FuzzyEquals 按容差判等 对应 Guava DoubleMath.fuzzyEquals
    internal static bool FuzzyEquals(double a, double b) => Math.Abs(a - b) < Epsilon;
}
