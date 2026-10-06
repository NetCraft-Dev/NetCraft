using NetCraft.Codec;

namespace NetCraft.Game.World.Level;

//GameType game mode, maps to vanilla net.minecraft.world.level.GameType
//Static instance pattern replaces enum, carries id/name/shortName plus capability flags
//id matches network protocol / Player.GameMode int: 0=survival 1=creative 2=adventure 3=spectator
public sealed class GameType
{
    //Survival can build and break
    public static readonly GameType Survival = new(0, "survival", "s");
    //Creative can fly, instabuild
    public static readonly GameType Creative = new(1, "creative", "c");
    //Adventure restricted block placing
    public static readonly GameType Adventure = new(2, "adventure", "a");
    //Spectator no collision, can fly, cannot interact
    public static readonly GameType Spectator = new(3, "spectator", "sp");

    //All all modes in ascending id order for iteration
    private static readonly GameType[] s_all = { Survival, Creative, Adventure, Spectator };

    //Codec encode/decode by name, maps to vanilla GameType.CODEC
    public static readonly Codec<GameType> Codec = Codecs.String.ComapFlatMap(
        name => ByName(name) is { } type
            ? DataResult<GameType>.Success(type)
            : DataResult<GameType>.Error(() => $"Unknown game mode: {name}"),
        type => type.Name);

    //Id numeric id, matches protocol
    public int Id { get; }
    //Name full name such as survival
    public string Name { get; }
    //ShortName short name such as s
    public string ShortName { get; }

    private GameType(int id, string name, string shortName)
    {
        Id = id;
        Name = name;
        ShortName = shortName;
    }

    //IsCreative creative or spectator, vanilla isCreative semantics
    public bool IsCreative => this == Creative || this == Spectator;
    //IsSurvival survival or adventure, vanilla isSurvival semantics
    public bool IsSurvival => this == Survival || this == Adventure;
    //IsBlockPlacingRestricted adventure or spectator restrict placing/breaking, vanilla isBlockPlacingRestricted semantics
    public bool IsBlockPlacingRestricted => this == Adventure || this == Spectator;
    //IsFlyAllowed creative or spectator can fly, vanilla isFlyAllowed semantics
    public bool IsFlyAllowed => this == Creative || this == Spectator;

    //ById lookup mode by numeric id, returns null if not found
    public static GameType? ById(int id)
    {
        foreach (var type in s_all)
            if (type.Id == id) return type;
        return null;
    }

    //ByName resolve by name or short name, case-insensitive, returns null if not found
    //vanilla byName iterates matching name/shortName equalsIgnoreCase
    public static GameType? ByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var type in s_all)
        {
            if (type.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                || type.ShortName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return type;
        }
        return null;
    }

    public override string ToString() => Name;
}
