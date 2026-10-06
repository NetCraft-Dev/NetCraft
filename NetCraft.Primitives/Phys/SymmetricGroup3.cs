namespace NetCraft.Primitives.Phys;

//SymmetricGroup3 symmetric group on three elements, maps to vanilla com.mojang.math.SymmetricGroup3
//The name lists the component order, e.g. P231 means taking (y,z,x) of the original vector
//The enum order must match the vanilla ordinal, the Cayley table indexes by ordinal
public enum SymmetricGroup3
{
    P123 = 0,
    P213 = 1,
    P132 = 2,
    P312 = 3,
    P231 = 4,
    P321 = 5,
}

//SymmetricGroup3Extensions permutation composition and inversion, maps to the methods and two static tables in the vanilla enum
public static class SymmetricGroup3Extensions
{
    private static readonly SymmetricGroup3[] Values =
    {
        SymmetricGroup3.P123,
        SymmetricGroup3.P213,
        SymmetricGroup3.P132,
        SymmetricGroup3.P312,
        SymmetricGroup3.P231,
        SymmetricGroup3.P321,
    };

    //The three component indices of each permutation, order corresponds one to one with Values
    private static readonly int[][] Components =
    {
        new[] { 0, 1, 2 },
        new[] { 1, 0, 2 },
        new[] { 0, 2, 1 },
        new[] { 2, 0, 1 },
        new[] { 1, 2, 0 },
        new[] { 2, 1, 0 },
    };

    private static readonly SymmetricGroup3[,] CayleyTable = BuildCayleyTable();

    private static readonly SymmetricGroup3[] InverseTable = BuildInverseTable();

    //Compose does that first then this, maps to the vanilla compose first.permute(second.p)
    public static SymmetricGroup3 Compose(this SymmetricGroup3 first, SymmetricGroup3 that)
        => CayleyTable[(int)first, (int)that];

    public static SymmetricGroup3 Inverse(this SymmetricGroup3 group) => InverseTable[(int)group];

    //Permute takes the i-th component index of the permutation, maps to vanilla permute
    public static int Permute(this SymmetricGroup3 group, int i)
    {
        if (i < 0 || i > 2) throw new ArgumentException($"Component index must be 0/1/2, got {i}");
        return Components[(int)group][i];
    }

    //PermuteAxis permutes an axis, the axis enum order matches the component indices, maps to vanilla permuteAxis
    public static Direction.Axis PermuteAxis(this SymmetricGroup3 group, Direction.Axis axis)
        => (Direction.Axis)group.Permute((int)axis);

    //PermuteVector permutes the vector components, maps to vanilla permuteVector
    public static Vec3i PermuteVector(this SymmetricGroup3 group, Vec3i v)
    {
        var components = Components[(int)group];
        var values = new[] { v.X, v.Y, v.Z };
        return new Vec3i(values[components[0]], values[components[1]], values[components[2]]);
    }

    private static SymmetricGroup3[,] BuildCayleyTable()
    {
        var size = Values.Length;
        var table = new SymmetricGroup3[size, size];
        for (var first = 0; first < size; first++)
        for (var second = 0; second < size; second++)
        {
            var p0 = Permute(Values[first], Components[second][0]);
            var p1 = Permute(Values[first], Components[second][1]);
            var p2 = Permute(Values[first], Components[second][2]);
            table[first, second] = Find(p0, p1, p2);
        }
        return table;
    }

    private static SymmetricGroup3[] BuildInverseTable()
    {
        var size = Values.Length;
        var table = new SymmetricGroup3[size];
        for (var i = 0; i < size; i++)
        {
            var found = false;
            for (var j = 0; j < size && !found; j++)
                if (Values[i].Compose(Values[j]) == SymmetricGroup3.P123)
                {
                    table[i] = Values[j];
                    found = true;
                }
            if (!found) throw new InvalidOperationException($"Permutation {Values[i]} has no inverse");
        }
        return table;
    }

    private static SymmetricGroup3 Find(int p0, int p1, int p2)
    {
        foreach (var group in Values)
        {
            var components = Components[(int)group];
            if (components[0] == p0 && components[1] == p1 && components[2] == p2) return group;
        }
        throw new InvalidOperationException($"No permutation matches ({p0},{p1},{p2})");
    }
}
