namespace NetCraft.Primitives;

//AABB 轴对齐包围盒对标原版 net.minecraft.world.phys.AABB
//Min/Max 两点定义盒体 Corners 返回 8 角点供 Frustum 剔除测试
//W5 首版仅含 Frustum 剔除所需接口 grow/intersect/move 等留后续
public readonly struct AABB
{
    //容差 形状体系判定坐标重合与射线命中都用它 对应原版 1.0E-7
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

    //Move 平移盒体 返回新盒体
    public AABB Move(Vec3 offset) => new(Min.Add(offset), Max.Add(offset));

    //Inflate 三轴各向外扩 用于范围查询与碰撞包边 对应原版 inflate
    public AABB Inflate(double x, double y, double z)
        => new(Min.Add(-x, -y, -z), Max.Add(x, y, z));

    //Deflate 三轴各向内收 对应原版 deflate
    public AABB Deflate(double amount) => Inflate(-amount, -amount, -amount);

    //ExpandTowards 按位移方向只朝该侧扩展 对应原版 expandTowards
    //碰撞查询要覆盖移动全程扫过的区域 不能两侧都扩
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

    //Size 盒体三轴边长
    public Vec3 Size => new(Max.X - Min.X, Max.Y - Min.Y, Max.Z - Min.Z);

    //Contains 点是否落在盒内 对应原版 contains
    public bool Contains(Vec3 point)
        => point.X >= Min.X && point.X < Max.X
            && point.Y >= Min.Y && point.Y < Max.Y
            && point.Z >= Min.Z && point.Z < Max.Z;

    //Intersects 判定两盒体是否相交
    public bool Intersects(AABB other) =>
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

    //Intersects 判定与给定坐标区间是否相交 对应原版六参重载
    //方块碰撞遍历拿格子当盒用 每次造 AABB 太浪费
    public bool Intersects(double minX, double minY, double minZ, double maxX, double maxY, double maxZ) =>
        Min.X <= maxX && Max.X >= minX &&
        Min.Y <= maxY && Max.Y >= minY &&
        Min.Z <= maxZ && Max.Z >= minZ;

    //Corners 返回 8 角点供 Frustum 8 角点测试或调试可视化
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

    //MinOf/MaxOf 按轴取小大坐标 对应原版 AABB.min(Axis)/max(Axis)
    public double MinOf(Direction.Axis axis) => axis.Choose(Min.X, Min.Y, Min.Z);

    public double MaxOf(Direction.Axis axis) => axis.Choose(Max.X, Max.Y, Max.Z);

    //Clip 射线与一组盒求交取最近的命中面 对应原版 AABB.clip
    //盒坐标按 pos 平移到方块本地 返回 null 表示都没命中
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

    //GetDirection 三个轴各试一次 命中的轴里取参数最小的那个 对应原版 getDirection
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

    //ClipPoint 求射线穿过某个面的参数 落在范围内且比已记录的更近才接受 对应原版 clipPoint
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
