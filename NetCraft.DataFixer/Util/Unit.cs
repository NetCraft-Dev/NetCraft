namespace NetCraft.DataFixer.Util;

//unit type maps to the vanilla Unit singleton
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Instance = default;

    public override string ToString() => "Unit";

    public bool Equals(Unit other) => true;

    public override bool Equals(object? obj) => obj is Unit;

    public override int GetHashCode() => 0;
}
