namespace NetCraft.Primitives;

//3D整型向量对应原版net.minecraft.core.Vec3i
//原版为可变类这里改为readonly struct值语义对齐C#惯用
//仅实现坐标运算和距离计算不含Codec与StreamCodec延后到Codec/Network接通
public readonly struct Vec3i : IEquatable<Vec3i>, IComparable<Vec3i>
{
    public static readonly Vec3i Zero = new(0, 0, 0);

    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public Vec3i(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    //compareTo按Y优先Z次X末排序对应原版compareTo
    public int CompareTo(Vec3i other)
    {
        if (Y == other.Y)
        {
            if (Z == other.Z) return X - other.X;
            return Z - other.Z;
        }
        return Y - other.Y;
    }

    //offset按相对偏移返回新Vec3i零偏移返回自身
    public Vec3i Offset(int x, int y, int z)
        => (x == 0 && y == 0 && z == 0) ? this : new Vec3i(X + x, Y + y, Z + z);

    public Vec3i Offset(Vec3i vec) => Offset(vec.X, vec.Y, vec.Z);

    public Vec3i Subtract(Vec3i vec) => Offset(-vec.X, -vec.Y, -vec.Z);

    //multiply按统一比例缩放scale为1返回自身为0返回ZERO
    public Vec3i Multiply(int scale)
    {
        if (scale == 1) return this;
        if (scale == 0) return Zero;
        return new Vec3i(X * scale, Y * scale, Z * scale);
    }

    public Vec3i Multiply(int xScale, int yScale, int zScale)
        => new(X * xScale, Y * yScale, Z * zScale);

    public Vec3i Above() => Above(1);
    public Vec3i Above(int steps) => Relative(Direction.Up, steps);
    public Vec3i Below() => Below(1);
    public Vec3i Below(int steps) => Relative(Direction.Down, steps);
    public Vec3i North() => North(1);
    public Vec3i North(int steps) => Relative(Direction.North, steps);
    public Vec3i South() => South(1);
    public Vec3i South(int steps) => Relative(Direction.South, steps);
    public Vec3i West() => West(1);
    public Vec3i West(int steps) => Relative(Direction.West, steps);
    public Vec3i East() => East(1);
    public Vec3i East(int steps) => Relative(Direction.East, steps);

    public Vec3i Relative(Direction direction) => Relative(direction, 1);

    //relative按方向与步数返回新偏移零步返回自身
    public Vec3i Relative(Direction direction, int steps)
    {
        if (steps == 0) return this;
        return new Vec3i(X + direction.StepX * steps, Y + direction.StepY * steps, Z + direction.StepZ * steps);
    }

    //relative按轴步进非该轴方向不动
    public Vec3i Relative(Direction.Axis axis, int steps)
    {
        if (steps == 0) return this;
        int xStep = axis == Direction.Axis.X ? steps : 0;
        int yStep = axis == Direction.Axis.Y ? steps : 0;
        int zStep = axis == Direction.Axis.Z ? steps : 0;
        return new Vec3i(X + xStep, Y + yStep, Z + zStep);
    }

    //cross叉积
    public Vec3i Cross(Vec3i other)
        => new(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);

    //closerThan距离平方小于阈值
    public bool CloserThan(Vec3i pos, double distance) => DistSqr(pos) < distance * distance;

    //distSqr到目标点最低角距离平方
    public double DistSqr(Vec3i pos)
    {
        double dx = X - pos.X;
        double dy = Y - pos.Y;
        double dz = Z - pos.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    //distToCenterSqr到目标点中心距离平方
    public double DistToCenterSqr(double x, double y, double z)
    {
        double dx = (X + 0.5) - x;
        double dy = (Y + 0.5) - y;
        double dz = (Z + 0.5) - z;
        return dx * dx + dy * dy + dz * dz;
    }

    public double DistManhattan(Vec3i other)
        => Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z);

    public override int GetHashCode() => ((Y + Z * 31) * 31) + X;

    public bool Equals(Vec3i other) => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is Vec3i v && Equals(v);

    public static bool operator ==(Vec3i left, Vec3i right) => left.Equals(right);
    public static bool operator !=(Vec3i left, Vec3i right) => !left.Equals(right);

    public override string ToString() => $"[{X}, {Y}, {Z}]";
}
