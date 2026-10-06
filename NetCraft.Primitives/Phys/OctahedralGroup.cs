namespace NetCraft.Primitives.Phys;

//OctahedralGroup octahedral group, maps to vanilla com.mojang.math.OctahedralGroup
//Each element is an axis permutation plus three axis-inversion switches, covering the 48 symmetry transforms of a cube
//The enum order must match the vanilla ordinal, the composition table indexes by ordinal
public enum OctahedralGroup
{
    Identity = 0,
    Rot180FaceXy = 1,
    Rot180FaceXz = 2,
    Rot180FaceYz = 3,
    Rot120Nnn = 4,
    Rot120Nnp = 5,
    Rot120Npn = 6,
    Rot120Npp = 7,
    Rot120Pnn = 8,
    Rot120Pnp = 9,
    Rot120Ppn = 10,
    Rot120Ppp = 11,
    Rot180EdgeXyNeg = 12,
    Rot180EdgeXyPos = 13,
    Rot180EdgeXzNeg = 14,
    Rot180EdgeXzPos = 15,
    Rot180EdgeYzNeg = 16,
    Rot180EdgeYzPos = 17,
    Rot90XNeg = 18,
    Rot90XPos = 19,
    Rot90YNeg = 20,
    Rot90YPos = 21,
    Rot90ZNeg = 22,
    Rot90ZPos = 23,
    Inversion = 24,
    InvertX = 25,
    InvertY = 26,
    InvertZ = 27,
    Rot60RefNnn = 28,
    Rot60RefNnp = 29,
    Rot60RefNpn = 30,
    Rot60RefNpp = 31,
    Rot60RefPnn = 32,
    Rot60RefPnp = 33,
    Rot60RefPpn = 34,
    Rot60RefPpp = 35,
    SwapXy = 36,
    SwapYz = 37,
    SwapXz = 38,
    SwapNegXy = 39,
    SwapNegYz = 40,
    SwapNegXz = 41,
    Rot90RefXNeg = 42,
    Rot90RefXPos = 43,
    Rot90RefYNeg = 44,
    Rot90RefYPos = 45,
    Rot90RefZNeg = 46,
    Rot90RefZPos = 47,
}

//OctahedralGroups composition, inversion and direction rotation of the octahedral group, maps to the methods and two static tables in the vanilla enum
public static class OctahedralGroups
{
    private const int Count = 48;

    //Permutation and three axis-inversion switches of each group, order corresponds one to one with the enum
    private static readonly (SymmetricGroup3 Perm, bool InvertX, bool InvertY, bool InvertZ)[] Entries =
    {
        (SymmetricGroup3.P123, false, false, false),
        (SymmetricGroup3.P123, true, true, false),
        (SymmetricGroup3.P123, true, false, true),
        (SymmetricGroup3.P123, false, true, true),
        (SymmetricGroup3.P231, false, false, false),
        (SymmetricGroup3.P312, true, false, true),
        (SymmetricGroup3.P312, false, true, true),
        (SymmetricGroup3.P231, true, false, true),
        (SymmetricGroup3.P312, true, true, false),
        (SymmetricGroup3.P231, true, true, false),
        (SymmetricGroup3.P231, false, true, true),
        (SymmetricGroup3.P312, false, false, false),
        (SymmetricGroup3.P213, true, true, true),
        (SymmetricGroup3.P213, false, false, true),
        (SymmetricGroup3.P321, true, true, true),
        (SymmetricGroup3.P321, false, true, false),
        (SymmetricGroup3.P132, true, true, true),
        (SymmetricGroup3.P132, true, false, false),
        (SymmetricGroup3.P132, false, false, true),
        (SymmetricGroup3.P132, false, true, false),
        (SymmetricGroup3.P321, true, false, false),
        (SymmetricGroup3.P321, false, false, true),
        (SymmetricGroup3.P213, false, true, false),
        (SymmetricGroup3.P213, true, false, false),
        (SymmetricGroup3.P123, true, true, true),
        (SymmetricGroup3.P123, true, false, false),
        (SymmetricGroup3.P123, false, true, false),
        (SymmetricGroup3.P123, false, false, true),
        (SymmetricGroup3.P312, true, true, true),
        (SymmetricGroup3.P231, true, false, false),
        (SymmetricGroup3.P231, false, false, true),
        (SymmetricGroup3.P312, false, false, true),
        (SymmetricGroup3.P231, false, true, false),
        (SymmetricGroup3.P312, true, false, false),
        (SymmetricGroup3.P312, false, true, false),
        (SymmetricGroup3.P231, true, true, true),
        (SymmetricGroup3.P213, false, false, false),
        (SymmetricGroup3.P132, false, false, false),
        (SymmetricGroup3.P321, false, false, false),
        (SymmetricGroup3.P213, true, true, false),
        (SymmetricGroup3.P132, false, true, true),
        (SymmetricGroup3.P321, true, false, true),
        (SymmetricGroup3.P132, true, false, true),
        (SymmetricGroup3.P132, true, true, false),
        (SymmetricGroup3.P321, true, true, false),
        (SymmetricGroup3.P321, false, true, true),
        (SymmetricGroup3.P213, false, true, true),
        (SymmetricGroup3.P213, true, false, true),
    };

    private static readonly OctahedralGroup[] Values = BuildValues();

    private static readonly OctahedralGroup[,] CayleyTable = BuildCayleyTable();

    private static readonly OctahedralGroup[] InverseTable = BuildInverseTable();

    //Mapping of each group over the six directions, direction rotation is used extremely often so it is precomputed
    private static readonly Direction[][] RotatedDirections = BuildRotatedDirections();

    public static readonly OctahedralGroup BlockRotX270 = OctahedralGroup.Rot90XPos;
    public static readonly OctahedralGroup BlockRotX180 = OctahedralGroup.Rot180FaceYz;
    public static readonly OctahedralGroup BlockRotX90 = OctahedralGroup.Rot90XNeg;
    public static readonly OctahedralGroup BlockRotY270 = OctahedralGroup.Rot90YPos;
    public static readonly OctahedralGroup BlockRotY180 = OctahedralGroup.Rot180FaceXz;
    public static readonly OctahedralGroup BlockRotY90 = OctahedralGroup.Rot90YNeg;
    public static readonly OctahedralGroup BlockRotZ270 = OctahedralGroup.Rot90ZPos;
    public static readonly OctahedralGroup BlockRotZ180 = OctahedralGroup.Rot180FaceXy;
    public static readonly OctahedralGroup BlockRotZ90 = OctahedralGroup.Rot90ZNeg;

    //Compose does that first then this, maps to vanilla compose
    public static OctahedralGroup Compose(this OctahedralGroup first, OctahedralGroup that)
        => CayleyTable[(int)first, (int)that];

    public static OctahedralGroup Inverse(this OctahedralGroup group) => InverseTable[(int)group];

    public static SymmetricGroup3 Permutation(this OctahedralGroup group) => Entries[(int)group].Perm;

    //Inverts whether the axis is negated in the transform, maps to vanilla inverts
    public static bool Inverts(this OctahedralGroup group, Direction.Axis axis) => axis switch
    {
        Direction.Axis.X => Entries[(int)group].InvertX,
        Direction.Axis.Y => Entries[(int)group].InvertY,
        _ => Entries[(int)group].InvertZ,
    };

    //Rotate the new direction after the transform, maps to vanilla rotate(Direction)
    //The new axis is the inverse of the permutation, negated or not is decided by the new axis
    public static Direction Rotate(this OctahedralGroup group, Direction direction)
        => RotatedDirections[(int)group][direction.Id3D];

    //Rotate the new vector after the transform, maps to vanilla rotate(Vector3i)
    public static Vec3i Rotate(this OctahedralGroup group, Vec3i v)
    {
        var permuted = Entries[(int)group].Perm.PermuteVector(v);
        return new Vec3i(
            Entries[(int)group].InvertX ? -permuted.X : permuted.X,
            Entries[(int)group].InvertY ? -permuted.Y : permuted.Y,
            Entries[(int)group].InvertZ ? -permuted.Z : permuted.Z);
    }

    //Trace shifts the permutation ordinal left by three bits then places the three axis-inversion bits, giving each group a fingerprint, maps to vanilla trace
    private static int Trace(bool invertX, bool invertY, bool invertZ, SymmetricGroup3 permutation)
    {
        var inversionIndex = (invertZ ? 4 : 0) + (invertY ? 2 : 0) + (invertX ? 1 : 0);
        return ((int)permutation << 3) | inversionIndex;
    }

    private static int Trace(OctahedralGroup group)
        => Trace(Entries[(int)group].InvertX, Entries[(int)group].InvertY, Entries[(int)group].InvertZ, Entries[(int)group].Perm);

    private static OctahedralGroup[] BuildValues()
    {
        var values = new OctahedralGroup[Count];
        for (var i = 0; i < Count; i++) values[i] = (OctahedralGroup)i;
        return values;
    }

    private static OctahedralGroup[,] BuildCayleyTable()
    {
        var fingerprints = new Dictionary<int, OctahedralGroup>();
        foreach (var group in Values) fingerprints[Trace(group)] = group;

        var table = new OctahedralGroup[Count, Count];
        for (var first = 0; first < Count; first++)
        for (var second = 0; second < Count; second++)
        {
            var firstPerm = Entries[first].Perm;
            var composedPermutation = Entries[second].Perm.Compose(firstPerm);
            var composedInvertX = Entries[first].InvertX ^ Inverts(second, firstPerm.PermuteAxis(Direction.Axis.X));
            var composedInvertY = Entries[first].InvertY ^ Inverts(second, firstPerm.PermuteAxis(Direction.Axis.Y));
            var composedInvertZ = Entries[first].InvertZ ^ Inverts(second, firstPerm.PermuteAxis(Direction.Axis.Z));
            table[first, second] = fingerprints[Trace(composedInvertX, composedInvertY, composedInvertZ, composedPermutation)];
        }
        return table;
    }

    private static OctahedralGroup[] BuildInverseTable()
    {
        var table = new OctahedralGroup[Count];
        for (var i = 0; i < Count; i++)
        {
            var found = false;
            for (var j = 0; j < Count && !found; j++)
                if (Values[i].Compose(Values[j]) == OctahedralGroup.Identity)
                {
                    table[i] = Values[j];
                    found = true;
                }
            if (!found) throw new InvalidOperationException($"Octahedral group {Values[i]} has no inverse");
        }
        return table;
    }

    private static Direction[][] BuildRotatedDirections()
    {
        var result = new Direction[Count][];
        for (var i = 0; i < Count; i++)
        {
            var array = new Direction[Direction.Values.Length];
            foreach (var facing in Direction.Values)
            {
                var oldDirection = facing.AxisDir;
                var newAxis = Entries[i].Perm.Inverse().PermuteAxis(facing.GetAxis());
                var newDirection = Inverts(i, newAxis) ? Flip(oldDirection) : oldDirection;
                array[facing.Id3D] = Direction.ByAxisDirection(newAxis, newDirection);
            }
            result[i] = array;
        }
        return result;
    }

    private static bool Inverts(int index, Direction.Axis axis) => axis switch
    {
        Direction.Axis.X => Entries[index].InvertX,
        Direction.Axis.Y => Entries[index].InvertY,
        _ => Entries[index].InvertZ,
    };

    private static Direction.AxisDirection Flip(Direction.AxisDirection direction)
        => direction == Direction.AxisDirection.Positive
            ? Direction.AxisDirection.Negative
            : Direction.AxisDirection.Positive;
}
