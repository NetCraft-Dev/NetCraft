namespace NetCraft.Primitives.Phys;

//Shapes shape factory and boolean operations, maps to vanilla Shapes
//Provides the three base shapes box/full-block/empty, plus the entry points to combine new shapes by boolean operations
//Rotation and mirror methods depend on OctahedralGroup and are left for V-4
public static partial class Shapes
{
    public const double Epsilon = 1.0E-7;
    public const double BigEpsilon = 1.0E-6;

    //DoubleLineConsumer callback for the two corners of a box or edge, maps to vanilla Shapes.DoubleLineConsumer
    public delegate void DoubleLineConsumer(double x1, double y1, double z1, double x2, double y2, double z2);

    private static readonly VoxelShape BlockShape = MakeBlock();

    //BlockCenter block center, used as the default rotation center
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
            throw new ArgumentException("Minimum must not be greater than maximum");
        return Create(minX, minY, minZ, maxX, maxY, maxZ);
    }

    //Create picks the compact representation based on whether it lands on 1/2/4/8 divisions, maps to vanilla create
    //If it divides evenly use the discrete cube grid, otherwise use coordinate arrays, both behave identically to the outside
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

    //FindBits the smallest division bit count that divides this coordinate evenly, returns -1 outside the unit cube, maps to vanilla findBits
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

    //Lcm least common multiple, maps to vanilla Shapes.lcm, returns long because multiplying the two grids may overflow int
    internal static long Lcm(int first, int second) => (long)first * (second / Gcd(first, second));

    //Gcd greatest common divisor, maps to Guava IntMath.gcd
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

    //Join runs a box merge after the boolean operation, maps to vanilla join
    public static VoxelShape Join(VoxelShape first, VoxelShape second, BooleanOp op)
        => JoinUnoptimized(first, second, op).Optimize();

    //JoinUnoptimized combines both shapes into one by a boolean operation, maps to vanilla joinUnoptimized
    //First pick a merger per axis, then apply op cell by cell on the discrete grid
    public static VoxelShape JoinUnoptimized(VoxelShape first, VoxelShape second, BooleanOp op)
    {
        //An operation that produces a solid when both sides are empty is meaningless, reject it up front to avoid index overruns later
        if (op(false, false)) throw new ArgumentException("This boolean operation produces a solid when both sides are empty");
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
        //Only when all three axes are evenly divided grids can the discrete representation be used, otherwise the coordinates are uneven and it must fall back to coordinate arrays
        if (xMerger is DiscreteCubeMerger && yMerger is DiscreteCubeMerger && zMerger is DiscreteCubeMerger)
            return new CubeVoxelShape(voxelShape);
        return new ArrayVoxelShape(voxelShape, xMerger.List, yMerger.List, zMerger.List);
    }

    //JoinIsNotEmpty only tests whether the boolean operation result is non-empty without building the shape, maps to vanilla joinIsNotEmpty
    //Much cheaper than Join, heavily used by light occlusion and collision pre-checks
    public static bool JoinIsNotEmpty(VoxelShape first, VoxelShape second, BooleanOp op)
    {
        if (op(false, false)) throw new ArgumentException("This boolean operation produces a solid when both sides are empty");

        var firstEmpty = first.IsEmpty;
        var secondEmpty = second.IsEmpty;
        if (firstEmpty || secondEmpty) return op(!firstEmpty, !secondEmpty);
        if (ReferenceEquals(first, second)) return op(true, true);

        var firstOnlyMatters = op(true, false);
        var secondOnlyMatters = op(false, true);
        //If fully separated on any axis the result is just the picks of each side
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

    //Collide advances a moving box along an axis and returns the actual displacement after being blocked by this group of shapes, maps to vanilla Shapes.collide
    public static double Collide(Direction.Axis axis, AABB moving, IEnumerable<VoxelShape> shapes, double distance)
    {
        foreach (var shape in shapes)
        {
            if (Math.Abs(distance) < Epsilon) return 0.0;
            distance = shape.Collide(axis, moving, distance);
        }
        return distance;
    }

    //BlockOccludes tests whether one face of a shape fully covers the whole face on the direction, maps to vanilla blockOccludes
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

    //MergedFaceOccludes whether the merged face has no gaps, maps to vanilla mergedFaceOccludes
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

    //Rotate rotates the shape by an octahedral group, maps to vanilla Shapes.rotate
    //After rotating the discrete cells the axes may swap, so the three coordinate sequences must be re-taken along the permuted axes
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

    //FlipAxisIfNeeded mirrors the coordinate sequence when the axis is negated, maps to vanilla flipAxisIfNeeded
    //When the rotation center and the old center project the same on this axis it is just a translation, no need to rebuild the sequence
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

    //RotateHorizontalAxis derives the X-axis shape from the Z-axis one, maps to vanilla rotateHorizontalAxis
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

    //RotateHorizontal derives the four horizontal directions from the north-facing shape, maps to vanilla rotateHorizontal
    //For NORTH vanilla keeps the default rotation center, copied as is here rather than changing it to rotationCenter
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

    //RotateAll derives all six directions from the north-facing shape, maps to vanilla rotateAll
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

    //CreateIndexMerger picks the cheapest merger based on the shapes of both coordinate sequences, maps to vanilla createIndexMerger
    //cost is the cell count already fixed on the current axis, used to estimate the merge result size, too small is not worth the discrete alignment
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

    //FuzzyEquals equality within tolerance, maps to Guava DoubleMath.fuzzyEquals
    internal static bool FuzzyEquals(double a, double b) => Math.Abs(a - b) < Epsilon;
}
