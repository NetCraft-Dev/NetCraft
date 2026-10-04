namespace NetCraft.Primitives.Phys;

//SymmetricGroup3 三元素置换群 对应原版 com.mojang.math.SymmetricGroup3
//下标是分量取值表 如 P231 表示取原向量的 (y,z,x)
//枚举顺序必须与原版 ordinal 一致 Cayley 表按下标取
public enum SymmetricGroup3
{
    P123 = 0,
    P213 = 1,
    P132 = 2,
    P312 = 3,
    P231 = 4,
    P321 = 5,
}

//SymmetricGroup3Extensions 置换的合成与求逆 对应原版枚举内的方法与两张静态表
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

    //每个置换的三个分量下标 顺序与 Values 一一对应
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

    //Compose 先做 that 再做 this 对应原版 compose 的 first.permute(second.p)
    public static SymmetricGroup3 Compose(this SymmetricGroup3 first, SymmetricGroup3 that)
        => CayleyTable[(int)first, (int)that];

    public static SymmetricGroup3 Inverse(this SymmetricGroup3 group) => InverseTable[(int)group];

    //Permute 取置换里第 i 个分量下标 对应原版 permute
    public static int Permute(this SymmetricGroup3 group, int i)
    {
        if (i < 0 || i > 2) throw new ArgumentException($"分量下标必须是0/1/2 收到{i}");
        return Components[(int)group][i];
    }

    //PermuteAxis 置换坐标轴 轴枚举顺序与分量下标一致 对应原版 permuteAxis
    public static Direction.Axis PermuteAxis(this SymmetricGroup3 group, Direction.Axis axis)
        => (Direction.Axis)group.Permute((int)axis);

    //PermuteVector 置换向量分量 对应原版 permuteVector
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
            if (!found) throw new InvalidOperationException($"置换{Values[i]}没有逆元");
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
        throw new InvalidOperationException($"没有匹配({p0},{p1},{p2})的置换");
    }
}
