using NetCraft.Codec;

namespace NetCraft.Registry;

//PermissionLevel 权限等级对应原版 net.minecraft.server.permissions.PermissionLevel
//原版把 0-4 的裸数字等级命名化 all 到 owners 值序与原版 id 一致
//StringRepresentable 与 ByIdMap.continuous(CLAMP) 的行为合并在序列化名与 ById 里
public enum PermissionLevel
{
    All = 0,
    Moderators = 1,
    Gamemasters = 2,
    Admins = 3,
    Owners = 4,
}

//PermissionLevel 静态成员与编解码
public static class PermissionLevels
{
    //Codec 按序列化名编解码对应原版 StringRepresentable.fromEnum
    public static readonly Codec<PermissionLevel> Codec = Codecs.String.ComapFlatMap(
        name => PermissionLevelExtensions.TryFromSerializedName(name)
            is { } level
            ? DataResult<PermissionLevel>.Success(level)
            : DataResult<PermissionLevel>.Error(() => $"Unknown permission level: {name}"),
        level => PermissionLevelExtensions.SerializedName(level));

    //IntCodec 按数字 id 编解码对应原版 Codec.INT.xmap(BY_ID)
    public static readonly Codec<PermissionLevel> IntCodec = Codecs.Int.ComapFlatMap(
        id => DataResult<PermissionLevel>.Success(PermissionLevelExtensions.ById(id)),
        level => (int)level);
}

public static class PermissionLevelExtensions
{
    //SerializedName 序列化名对应原版 getSerializedName
    public static string SerializedName(this PermissionLevel level) => level switch
    {
        PermissionLevel.All => "all",
        PermissionLevel.Moderators => "moderators",
        PermissionLevel.Gamemasters => "gamemasters",
        PermissionLevel.Admins => "admins",
        PermissionLevel.Owners => "owners",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    //TryFromSerializedName 按序列化名反查
    public static PermissionLevel? TryFromSerializedName(string name) => name switch
    {
        "all" => PermissionLevel.All,
        "moderators" => PermissionLevel.Moderators,
        "gamemasters" => PermissionLevel.Gamemasters,
        "admins" => PermissionLevel.Admins,
        "owners" => PermissionLevel.Owners,
        _ => null,
    };

    //ById 按 id 取等级越界钳制到两端对应原版 ByIdMap 的 OutOfBoundsStrategy.CLAMP
    public static PermissionLevel ById(int id)
        => (PermissionLevel)Math.Clamp(id, (int)PermissionLevel.All, (int)PermissionLevel.Owners);

    //IsEqualOrHigherThan 等级不低于另一等级对应原版 isEqualOrHigherThan
    public static bool IsEqualOrHigherThan(this PermissionLevel level, PermissionLevel other)
        => level >= other;
}
