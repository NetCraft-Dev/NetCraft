namespace NetCraft.Primitives;

//全局位置对应原版net.minecraft.core.GlobalPos
//原版为record持可选ResourceKey<Level>维度与BlockPos
//此处用readonly struct值语义dimensionKey用object占位待Registry就绪后改ResourceKey
public readonly struct GlobalPos : IEquatable<GlobalPos>
{
    public static readonly GlobalPos Zero = new(null, BlockPos.Zero);

    public object? DimensionKey { get; }
    public BlockPos Pos { get; }

    public GlobalPos(object? dimensionKey, BlockPos pos)
    {
        DimensionKey = dimensionKey;
        Pos = pos;
    }

    //of按dimension+pos构造
    public static GlobalPos Of(object? dimensionKey, BlockPos pos) => new(dimensionKey, pos);

    public override int GetHashCode() => HashCode.Combine(DimensionKey, Pos);

    public bool Equals(GlobalPos other) => Equals(DimensionKey, other.DimensionKey) && Pos == other.Pos;

    public override bool Equals(object? obj) => obj is GlobalPos g && Equals(g);

    public static bool operator ==(GlobalPos left, GlobalPos right) => left.Equals(right);
    public static bool operator !=(GlobalPos left, GlobalPos right) => !left.Equals(right);

    public override string ToString() => $"[{DimensionKey}] {Pos}";
}
