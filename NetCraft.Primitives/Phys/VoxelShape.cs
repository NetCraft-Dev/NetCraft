namespace NetCraft.Primitives.Phys;

//VoxelShape voxel shape, maps to vanilla VoxelShape
//Unified representation of block collision shapes, backed by discrete cells, coordinates come from subclasses
//Exposes bounding box, box decomposition, ray intersection and axis-aligned collision
public abstract class VoxelShape
{
    //faces caches the slices computed for the four sides by direction, maps to vanilla faces
    private VoxelShape[]? _faces;

    protected VoxelShape(DiscreteVoxelShape shape) => Shape = shape;

    //Shape the discrete cell representation, visible within the assembly and to derived classes
    protected internal readonly DiscreteVoxelShape Shape;

    public abstract IReadOnlyList<double> GetCoords(Direction.Axis axis);

    public double Min(Direction.Axis axis)
    {
        var index = Shape.FirstFull(axis);
        return index >= Shape.GetSize(axis) ? double.PositiveInfinity : Get(axis, index);
    }

    public double Max(Direction.Axis axis)
    {
        var index = Shape.LastFull(axis);
        return index <= 0 ? double.NegativeInfinity : Get(axis, index);
    }

    public AABB Bounds() => IsEmpty
        ? throw new InvalidOperationException("An empty shape has no bounding box")
        : new AABB(Min(Direction.Axis.X), Min(Direction.Axis.Y), Min(Direction.Axis.Z),
            Max(Direction.Axis.X), Max(Direction.Axis.Y), Max(Direction.Axis.Z));

    //SingleEncompassing encloses the whole shape in one box, maps to vanilla singleEncompassing
    public VoxelShape SingleEncompassing() => IsEmpty
        ? Shapes.Empty()
        : Shapes.Box(Min(Direction.Axis.X), Min(Direction.Axis.Y), Min(Direction.Axis.Z),
            Max(Direction.Axis.X), Max(Direction.Axis.Y), Max(Direction.Axis.Z));

    protected double Get(Direction.Axis axis, int index) => GetCoords(axis)[index];

    public bool IsEmpty => Shape.IsEmpty();

    public VoxelShape Move(Vec3 delta) => Move(delta.X, delta.Y, delta.Z);

    public VoxelShape Move(Vec3i delta) => Move(delta.X, delta.Y, delta.Z);

    //Move translates the shape, coordinates get a uniform offset added, no need to rebuild discrete cells, maps to vanilla move
    public VoxelShape Move(double dx, double dy, double dz) => IsEmpty
        ? Shapes.Empty()
        : new ArrayVoxelShape(Shape,
            new OffsetDoubleList(GetCoords(Direction.Axis.X), dx),
            new OffsetDoubleList(GetCoords(Direction.Axis.Y), dy),
            new OffsetDoubleList(GetCoords(Direction.Axis.Z), dz));

    //Optimize merges discrete cells into as few boxes as possible then rebuilds, maps to vanilla optimize
    //Boolean operation results are often a pile of fragmented cells, one pass significantly reduces later iteration cost
    public VoxelShape Optimize()
    {
        var result = Shapes.Empty();
        ForAllBoxes((x1, y1, z1, x2, y2, z2) =>
            result = Shapes.JoinUnoptimized(result, Shapes.Box(x1, y1, z1, x2, y2, z2), BooleanOps.Or));
        return result;
    }

    public void ForAllEdges(Shapes.DoubleLineConsumer consumer)
        => Shape.ForAllEdges((xi1, yi1, zi1, xi2, yi2, zi2) => consumer(
            Get(Direction.Axis.X, xi1), Get(Direction.Axis.Y, yi1), Get(Direction.Axis.Z, zi1),
            Get(Direction.Axis.X, xi2), Get(Direction.Axis.Y, yi2), Get(Direction.Axis.Z, zi2)), true);

    public void ForAllBoxes(Shapes.DoubleLineConsumer consumer)
    {
        var xCoords = GetCoords(Direction.Axis.X);
        var yCoords = GetCoords(Direction.Axis.Y);
        var zCoords = GetCoords(Direction.Axis.Z);
        Shape.ForAllBoxes((xi1, yi1, zi1, xi2, yi2, zi2) => consumer(
            xCoords[xi1], yCoords[yi1], zCoords[zi1],
            xCoords[xi2], yCoords[yi2], zCoords[zi2]), true);
    }

    public List<AABB> ToAabbs()
    {
        var boxes = new List<AABB>();
        ForAllBoxes((x1, y1, z1, x2, y2, z2) => boxes.Add(new AABB(x1, y1, z1, x2, y2, z2)));
        return boxes;
    }

    //Min/Max fix the other two axes' coordinates and find the solid range along aAxis only, maps to the vanilla three-argument min/max
    public double Min(Direction.Axis aAxis, double b, double c)
    {
        var bAxis = AxisCycle.Forward.Cycle(aAxis);
        var cAxis = AxisCycle.Backward.Cycle(aAxis);
        var index = Shape.FirstFull(aAxis, FindIndex(bAxis, b), FindIndex(cAxis, c));
        return index >= Shape.GetSize(aAxis) ? double.PositiveInfinity : Get(aAxis, index);
    }

    public double Max(Direction.Axis aAxis, double b, double c)
    {
        var bAxis = AxisCycle.Forward.Cycle(aAxis);
        var cAxis = AxisCycle.Backward.Cycle(aAxis);
        var index = Shape.LastFull(aAxis, FindIndex(bAxis, b), FindIndex(cAxis, c));
        return index <= 0 ? double.NegativeInfinity : Get(aAxis, index);
    }

    //FindIndex binary searches which cell a coordinate falls into, maps to vanilla findIndex
    protected virtual int FindIndex(Direction.Axis axis, double coord)
        => LowerBound(0, Shape.GetSize(axis) + 1, index => coord < Get(axis, index)) - 1;

    //LowerBound the first index where the predicate holds, maps to Mth.binarySearch
    private static int LowerBound(int from, int to, Func<int, bool> predicate)
    {
        var low = from;
        var high = to - 1;
        while (low <= high)
        {
            var mid = (low + high) >> 1;
            if (predicate(mid)) high = mid - 1;
            else low = mid + 1;
        }
        return high + 1;
    }

    //Clip intersects a ray with the shape, when the origin is already inside it reports an inside hit, maps to vanilla clip
    public BlockHitResult? Clip(Vec3 from, Vec3 to, BlockPos pos)
    {
        if (IsEmpty) return null;
        var diff = to.Subtract(from);
        if (diff.LengthSqr() < Shapes.Epsilon) return null;
        var testPoint = from.Add(diff.Multiply(0.001));
        if (Shape.IsFullWide(
                FindIndex(Direction.Axis.X, testPoint.X - pos.X),
                FindIndex(Direction.Axis.Y, testPoint.Y - pos.Y),
                FindIndex(Direction.Axis.Z, testPoint.Z - pos.Z)))
        {
            var nearest = Direction.FromViewVector(diff.X, diff.Y, diff.Z).Opposite;
            return new BlockHitResult(pos, nearest, testPoint, true);
        }
        return AABB.Clip(ToAabbs(), from, to, pos);
    }

    public Vec3? ClosestPointTo(Vec3 point)
    {
        if (IsEmpty) return null;
        Vec3? closest = null;
        ForAllBoxes((x1, y1, z1, x2, y2, z2) =>
        {
            var x = Math.Clamp(point.X, x1, x2);
            var y = Math.Clamp(point.Y, y1, y2);
            var z = Math.Clamp(point.Z, z1, z2);
            var candidate = new Vec3(x, y, z);
            if (closest is not { } current || point.DistanceToSqr(candidate) < point.DistanceToSqr(current))
                closest = candidate;
        });
        return closest;
    }

    //GetFaceShape takes the face facing a direction, maps to vanilla getFaceShape
    //Textures and lighting only need that single slice, cache it after computing
    public VoxelShape GetFaceShape(Direction direction)
    {
        if (IsEmpty || ReferenceEquals(this, Shapes.Block())) return this;
        if (_faces is not null)
        {
            var cached = _faces[direction.Id3D];
            if (cached is not null) return cached;
        }
        else
        {
            _faces = new VoxelShape[6];
        }

        var face = CalculateFace(direction);
        _faces[direction.Id3D] = face;
        return face;
    }

    private VoxelShape CalculateFace(Direction direction)
    {
        var axis = direction.GetAxis();
        if (IsCubeLikeAlong(axis)) return this;
        var sign = direction.AxisDir;
        var index = FindIndex(axis, sign == Direction.AxisDirection.Positive ? 0.9999999 : 1.0E-7);
        var slice = new SliceShape(this, axis, index);
        if (slice.IsEmpty) return Shapes.Empty();
        if (slice.IsCubeLike()) return Shapes.Block();
        return slice;
    }

    protected bool IsCubeLike()
    {
        foreach (var axis in ShapeAxes)
            if (!IsCubeLikeAlong(axis)) return false;
        return true;
    }

    private bool IsCubeLikeAlong(Direction.Axis axis)
    {
        var coords = GetCoords(axis);
        return coords.Count == 2
            && Shapes.FuzzyEquals(coords[0], 0.0)
            && Shapes.FuzzyEquals(coords[1], 1.0);
    }

    private static readonly Direction.Axis[] ShapeAxes =
    {
        Direction.Axis.X,
        Direction.Axis.Y,
        Direction.Axis.Z,
    };

    //Collide advances a box along an axis and returns the actual displacement after being blocked by the shape, maps to vanilla collide
    public double Collide(Direction.Axis axis, AABB moving, double distance)
        => CollideX(AxisCycle.Between(axis, Direction.Axis.X), moving, distance);

    protected double CollideX(AxisCycle transform, AABB moving, double distance)
    {
        if (IsEmpty) return distance;
        if (Math.Abs(distance) < Shapes.Epsilon) return 0.0;

        var inverse = transform.Inverse;
        var aAxis = inverse.Cycle(Direction.Axis.X);
        var bAxis = inverse.Cycle(Direction.Axis.Y);
        var cAxis = inverse.Cycle(Direction.Axis.Z);
        var maxA = moving.MaxOf(aAxis);
        var minA = moving.MinOf(aAxis);
        var aMin = FindIndex(aAxis, minA + Shapes.Epsilon);
        var aMax = FindIndex(aAxis, maxA - Shapes.Epsilon);
        var bMin = Math.Max(0, FindIndex(bAxis, moving.MinOf(bAxis) + Shapes.Epsilon));
        var bMax = Math.Min(Shape.GetSize(bAxis), FindIndex(bAxis, moving.MaxOf(bAxis) - Shapes.Epsilon) + 1);
        var cMin = Math.Max(0, FindIndex(cAxis, moving.MinOf(cAxis) + Shapes.Epsilon));
        var cMax = Math.Min(Shape.GetSize(cAxis), FindIndex(cAxis, moving.MaxOf(cAxis) - Shapes.Epsilon) + 1);
        var aSize = Shape.GetSize(aAxis);

        if (distance > 0.0)
        {
            for (var a = aMax + 1; a < aSize; a++)
            for (var b = bMin; b < bMax; b++)
            for (var c = cMin; c < cMax; c++)
            {
                if (!Shape.IsFullWide(inverse, a, b, c)) continue;
                var newDistance = Get(aAxis, a) - maxA;
                if (newDistance >= -Shapes.Epsilon) distance = Math.Min(distance, newDistance);
                return distance;
            }
        }
        else if (distance < 0.0)
        {
            for (var a = aMin - 1; a >= 0; a--)
            for (var b = bMin; b < bMax; b++)
            for (var c = cMin; c < cMax; c++)
            {
                if (!Shape.IsFullWide(inverse, a, b, c)) continue;
                var newDistance = Get(aAxis, a + 1) - minA;
                if (newDistance <= Shapes.Epsilon) distance = Math.Max(distance, newDistance);
                return distance;
            }
        }
        return distance;
    }

    public override string ToString() => IsEmpty ? "EMPTY" : $"VoxelShape[{Bounds()}]";
}
