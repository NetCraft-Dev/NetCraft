namespace NetCraft.Registry;

//LevelBasedPermissionSet 按等级判定的权限集合对应原版 net.minecraft.server.permissions.LevelBasedPermissionSet
//原版是带五个单例的接口 这里是密封类 命令等级门槛直接比数字
//非等级权限只认 entity_selectors 一个 其余 Atom 一律不通过 与原版一致
public class LevelBasedPermissionSet : PermissionSet
{
    public static readonly LevelBasedPermissionSet All = Create(PermissionLevel.All);
    public static readonly LevelBasedPermissionSet Moderator = Create(PermissionLevel.Moderators);
    public static readonly LevelBasedPermissionSet Gamemaster = Create(PermissionLevel.Gamemasters);
    public static readonly LevelBasedPermissionSet Admin = Create(PermissionLevel.Admins);
    public static readonly LevelBasedPermissionSet Owner = Create(PermissionLevel.Owners);

    public PermissionLevel Level { get; }

    private LevelBasedPermissionSet(PermissionLevel level) => Level = level;

    public override bool HasPermission(Permission permission)
    {
        if (permission is Permission.HasCommandLevel levelCheck)
            return Level.IsEqualOrHigherThan(levelCheck.Level);
        if (permission.Equals(Permissions.CommandsEntitySelectors))
            return Level.IsEqualOrHigherThan(PermissionLevel.Gamemasters);
        return false;
    }

    public override PermissionSet Union(PermissionSet other)
    {
        //并集保留等级较高的一方 CFR 反编译把条件翻转了 按语义归位
        if (other is LevelBasedPermissionSet otherSet)
            return Level.IsEqualOrHigherThan(otherSet.Level) ? this : otherSet;
        return base.Union(other);
    }

    //ForLevel 按等级取单例对应原版 forLevel
    public static LevelBasedPermissionSet ForLevel(PermissionLevel level) => level switch
    {
        PermissionLevel.All => All,
        PermissionLevel.Moderators => Moderator,
        PermissionLevel.Gamemasters => Gamemaster,
        PermissionLevel.Admins => Admin,
        PermissionLevel.Owners => Owner,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    private static LevelBasedPermissionSet Create(PermissionLevel level) => new(level);

    public override string ToString() => $"permission level: {Level.SerializedName()}";
}
