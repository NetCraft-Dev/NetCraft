namespace NetCraft.Primitives;

//AABB axis-aligned bounding box, mirrors vanilla net.minecraft.world.phys.AABB
//Min/Max define the box, Corners returns the 8 corners for Frustum culling tests
//W5 first version only contains the interfaces needed by Frustum culling, grow/intersect/move and such are left for later
public readonly struct AABB
{
    //Tolerance used by the shape system to test coordinate coincidence and ray hits, maps to vanilla 1.0E-7
    private const double Tolerance = 1.0E-7;

    public readonly Vec3 Min;
    public readonly Vec3 Max;

    public AABB(Vec3 min, Vec3 max)
    {
        Min = min;
        Max = max;
    }

    public AABB(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        : this(new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ)) { }

    //Move translates the box, returns a new box
    public AABB Move(Vec3 offset) => new(Min.Add(offset), Max.Add(offset));

    //Inflate expands all three axes outward, used for range queries and collision padding, maps to vanilla inflate
    public AABB Inflate(double x, double y, double z)
        => new(Min.Add(-x, -y, -z), Max.Add(x, y, z));

    //Deflate shrinks all three axes inward, maps to vanilla deflate
    public AABB Deflate(double amount) => Inflate(-amount, -amount, -amount);

    //ExpandTowards expands only toward the side of the movement, maps to vanilla expandTowards
    //Collision queries must cover the whole swept region of the movement, so it cannot expand both sides
    public AABB ExpandTowards(Vec3 movement)
    {
        var minX = movement.X < 0 ? movement.X : 0;
        var minY = movement.Y < 0 ? movement.Y : 0;
        var minZ = movement.Z < 0 ? movement.Z : 0;
        var maxX = movement.X > 0 ? movement.X : 0;
        var maxY = movement.Y > 0 ? movement.Y : 0;
        var maxZ = movement.Z > 0 ? movement.Z : 0;
        return new AABB(Min.X + minX, Min.Y + minY, Min.Z + minZ, Max.X + maxX, Max.Y + maxY, Max.Z + maxZ);
    }

    //Size edge lengths of the box along the three axes
    public Vec3 Size => new(Max.X - Min.X, Max.Y - Min.Y, Max.Z - Min.Z);

    //Contains whether a point lies inside the box, maps to vanilla contains
    public bool Contains(Vec3 point)
        => point.X >= Min.X && point.X < Max.X
            && point.Y >= Min.Y && point.Y < Max.Y
            && point.Z >= Min.Z && point.Z < Max.Z;

    //Intersects whether two boxes intersect
    public bool Intersects(AABB other) =>
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

    //Intersects whether it intersects the given coordinate range, maps to the vanilla six-argument overload
    //Block collision iteration treats cells as boxes, allocating an AABB each time is wasteful
    public bool Intersects(double minX, double minY, double minZ, double maxX, double maxY, double maxZ) =>
        Min.X <= maxX && Max.X >= minX &&
        Min.Y <= maxY && Max.Y >= minY &&
        Min.Z <= maxZ && Max.Z >= minZ;

    //Corners returns the 8 corner points for the Frustum corner test or debug visualization
    public Vec3[] Corners() => new[]
    {
        Min,
        new Vec3(Max.X, Min.Y, Min.Z),
        new Vec3(Min.X, Max.Y, Min.Z),
        new Vec3(Max.X, Max.Y, Min.Z),
        new Vec3(Min.X, Min.Y, Max.Z),
        new Vec3(Max.X, Min.Y, Max.Z),
        new Vec3(Min.X, Max.Y, Max.Z),
        Max
    };

    //MinOf/MaxOf take the min/max coordinate along the axis, maps to vanilla AABB.min(Axis)/max(Axis)
    public double MinOf(Direction.Axis axis) => axis.Choose(Min.X, Min.Y, Min.Z);

    public double MaxOf(Direction.Axis axis) => axis.Choose(Max.X, Max.Y, Max.Z);

    //Clip intersects a ray with a group of boxes and takes the nearest hit face, maps to vanilla AABB.clip
    //Box coordinates are translated to block-local by pos, returning null means nothing was hit
    public static BlockHitResult? Clip(IEnumerable<AABB> boxes, Vec3 from, Vec3 to, BlockPos pos)
    {
        var scale = 1.0;
        Direction? hitDirection = null;
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var dz = to.Z - from.Z;
        var offset = new Vec3(pos.X, pos.Y, pos.Z);
        foreach (var box in boxes)
            hitDirection = GetDirection(box.Move(offset), from, ref scale, hitDirection, dx, dy, dz);
        if (hitDirection is not { } direction) return null;
        return new BlockHitResult(pos, direction,
            from.Add(scale * dx, scale * dy, scale * dz), false);
    }

    //GetDirection tries each of the three axes, among the hit axes takes the one with the smallest parameter, maps to vanilla getDirection
    private static Direction? GetDirection(AABB box, Vec3 from, ref double scale,
        Direction? direction, double dx, double dy, double dz)
    {
        if (dx > Tolerance)
            direction = ClipPoint(ref scale, direction, dx, dy, dz, box.Min.X, box.Min.Y, box.Max.Y,
                box.Min.Z, box.Max.Z, Direction.West, from.X, from.Y, from.Z);
        else if (dx < -Tolerance)
            direction = ClipPoint(ref scale, direction, dx, dy, dz, box.Max.X, box.Min.Y, box.Max.Y,
                box.Min.Z, box.Max.Z, Direction.East, from.X, from.Y, from.Z);

        if (dy > Tolerance)
            direction = ClipPoint(ref scale, direction, dy, dz, dx, box.Min.Y, box.Min.Z, box.Max.Z,
                box.Min.X, box.Max.X, Direction.Down, from.Y, from.Z, from.X);
        else if (dy < -Tolerance)
            direction = ClipPoint(ref scale, direction, dy, dz, dx, box.Max.Y, box.Min.Z, box.Max.Z,
                box.Min.X, box.Max.X, Direction.Up, from.Y, from.Z, from.X);

        if (dz > Tolerance)
            direction = ClipPoint(ref scale, direction, dz, dx, dy, box.Min.Z, box.Min.X, box.Max.X,
                box.Min.Y, box.Max.Y, Direction.North, from.Z, from.X, from.Y);
        else if (dz < -Tolerance)
            direction = ClipPoint(ref scale, direction, dz, dx, dy, box.Max.Z, box.Min.X, box.Max.X,
                box.Min.Y, box.Max.Y, Direction.South, from.Z, from.X, from.Y);

        return direction;
    }

    //ClipPoint solves the ray parameter through a face, accepted only if it is within range and closer than the recorded one, maps to vanilla clipPoint
    private static Direction? ClipPoint(ref double scale, Direction? direction, double da, double db, double dc,
        double point, double minB, double maxB, double minC, double maxC,
        Direction newDirection, double fromA, double fromB, double fromC)
    {
        var s = (point - fromA) / da;
        var pb = fromB + s * db;
        var pc = fromC + s * dc;
        if (s > 0.0 && s < scale
            && pb > minB - Tolerance && pb < maxB + Tolerance
            && pc > minC - Tolerance && pc < maxC + Tolerance)
        {
            scale = s;
            return newDirection;
        }
        return direction;
    }
}
