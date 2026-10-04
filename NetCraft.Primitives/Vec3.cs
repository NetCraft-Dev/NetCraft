namespace NetCraft.Primitives;

//3D浮点向量对应原版net.minecraft.world.phys.Vec3
//readonly struct值语义仅实现坐标运算和距离计算不含Codec延后到Codec接通
public readonly struct Vec3 : IEquatable<Vec3>
{
    public static readonly Vec3 Zero = new(0, 0, 0);

    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public Vec3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vec3 Add(Vec3 other) => new(X + other.X, Y + other.Y, Z + other.Z);
    public Vec3 Add(double x, double y, double z) => new(X + x, Y + y, Z + z);

    public Vec3 Subtract(Vec3 other) => new(X - other.X, Y - other.Y, Z - other.Z);

    public Vec3 Multiply(double scale) => new(X * scale, Y * scale, Z * scale);

    //WithAxis 把某轴分量换成新值 其余两轴保持 对应原版 Vec3.with(Axis)
    //逐轴裁剪位移时用它把解出来的那一轴写回
    public Vec3 WithAxis(Direction.Axis axis, double value) => axis switch
    {
        Direction.Axis.X => new Vec3(value, Y, Z),
        Direction.Axis.Y => new Vec3(X, value, Z),
        _ => new Vec3(X, Y, value),
    };

    public Vec3 Normalize()
    {
        var len = Length();
        return len < 1E-5 ? Zero : new Vec3(X / len, Y / len, Z / len);
    }

    public double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);

    public double LengthSqr() => X * X + Y * Y + Z * Z;

    //distanceTo到目标距离
    public double DistanceTo(Vec3 other) => Subtract(other).Length();

    public double DistanceToSqr(Vec3 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is Vec3 v && Equals(v);

    public static bool operator ==(Vec3 left, Vec3 right) => left.Equals(right);
    public static bool operator !=(Vec3 left, Vec3 right) => !left.Equals(right);

    public override string ToString() => $"[{X}, {Y}, {Z}]";
}
