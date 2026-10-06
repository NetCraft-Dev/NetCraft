namespace NetCraft.Primitives;

//3D floating point vector, maps to vanilla net.minecraft.world.phys.Vec3
//readonly struct value semantics, only coordinate math and distance calculation, no Codec yet, deferred until Codec is wired up
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

    //WithAxis replaces the component of one axis with a new value, the other two stay, maps to vanilla Vec3.with(Axis)
    //Used to write back the solved axis when clipping movement axis by axis
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

    //distanceTo distance to the target
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
