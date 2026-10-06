namespace NetCraft.Registry;

//LevelBasedPermissionSet level-based permission set, maps to vanilla net.minecraft.server.permissions.LevelBasedPermissionSet
//Vanilla is an interface with five singletons; here it is a sealed class and the command level threshold compares numbers directly
//Non-level permissions accept only entity_selectors and reject all other Atom permissions, matching vanilla
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
        //The union keeps the higher level; the CFR decompile had the condition flipped and it is restored here by semantics
        if (other is LevelBasedPermissionSet otherSet)
            return Level.IsEqualOrHigherThan(otherSet.Level) ? this : otherSet;
        return base.Union(other);
    }

    //ForLevel gets the singleton by level, maps to vanilla forLevel
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
