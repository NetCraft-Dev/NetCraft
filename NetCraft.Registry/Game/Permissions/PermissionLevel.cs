using NetCraft.Codec;

namespace NetCraft.Registry;

//PermissionLevel permission level, maps to vanilla net.minecraft.server.permissions.PermissionLevel
//Vanilla names the bare numeric levels 0-4 from all to owners, with values matching vanilla ids
//StringRepresentable and ByIdMap.continuous(CLAMP) behavior are merged into the serialized name and ById
public enum PermissionLevel
{
    All = 0,
    Moderators = 1,
    Gamemasters = 2,
    Admins = 3,
    Owners = 4,
}

//PermissionLevel static members and codecs
public static class PermissionLevels
{
    //Codec encodes/decodes by serialized name, maps to vanilla StringRepresentable.fromEnum
    public static readonly Codec<PermissionLevel> Codec = Codecs.String.ComapFlatMap(
        name => PermissionLevelExtensions.TryFromSerializedName(name)
            is { } level
            ? DataResult<PermissionLevel>.Success(level)
            : DataResult<PermissionLevel>.Error(() => $"Unknown permission level: {name}"),
        level => PermissionLevelExtensions.SerializedName(level));

    //IntCodec encodes/decodes by numeric id, maps to vanilla Codec.INT.xmap(BY_ID)
    public static readonly Codec<PermissionLevel> IntCodec = Codecs.Int.ComapFlatMap(
        id => DataResult<PermissionLevel>.Success(PermissionLevelExtensions.ById(id)),
        level => (int)level);
}

public static class PermissionLevelExtensions
{
    //SerializedName serialized name, maps to vanilla getSerializedName
    public static string SerializedName(this PermissionLevel level) => level switch
    {
        PermissionLevel.All => "all",
        PermissionLevel.Moderators => "moderators",
        PermissionLevel.Gamemasters => "gamemasters",
        PermissionLevel.Admins => "admins",
        PermissionLevel.Owners => "owners",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    //TryFromSerializedName reverse lookup by serialized name
    public static PermissionLevel? TryFromSerializedName(string name) => name switch
    {
        "all" => PermissionLevel.All,
        "moderators" => PermissionLevel.Moderators,
        "gamemasters" => PermissionLevel.Gamemasters,
        "admins" => PermissionLevel.Admins,
        "owners" => PermissionLevel.Owners,
        _ => null,
    };

    //ById gets a level by id, clamping out-of-range to the bounds, maps to vanilla ByIdMap's OutOfBoundsStrategy.CLAMP
    public static PermissionLevel ById(int id)
        => (PermissionLevel)Math.Clamp(id, (int)PermissionLevel.All, (int)PermissionLevel.Owners);

    //IsEqualOrHigherThan whether the level is at least another, maps to vanilla isEqualOrHigherThan
    public static bool IsEqualOrHigherThan(this PermissionLevel level, PermissionLevel other)
        => level >= other;
}
