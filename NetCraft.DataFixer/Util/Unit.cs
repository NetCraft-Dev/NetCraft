namespace NetCraft.DataFixer.Util;

//单元类型对应原版Unit单例
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Instance = default;

    public override string ToString() => "Unit";

    public bool Equals(Unit other) => true;

    public override bool Equals(object? obj) => obj is Unit;

    public override int GetHashCode() => 0;
}
