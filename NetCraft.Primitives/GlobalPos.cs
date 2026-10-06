namespace NetCraft.Primitives;

//Global position, maps to vanilla net.minecraft.core.GlobalPos
//Vanilla is a record holding an optional ResourceKey<Level> dimension and a BlockPos
//Here a readonly struct for value semantics, dimensionKey uses object as a placeholder until the Registry is ready to switch to ResourceKey
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

    //of constructs from dimension + pos
    public static GlobalPos Of(object? dimensionKey, BlockPos pos) => new(dimensionKey, pos);

    public override int GetHashCode() => HashCode.Combine(DimensionKey, Pos);

    public bool Equals(GlobalPos other) => Equals(DimensionKey, other.DimensionKey) && Pos == other.Pos;

    public override bool Equals(object? obj) => obj is GlobalPos g && Equals(g);

    public static bool operator ==(GlobalPos left, GlobalPos right) => left.Equals(right);
    public static bool operator !=(GlobalPos left, GlobalPos right) => !left.Equals(right);

    public override string ToString() => $"[{DimensionKey}] {Pos}";
}
