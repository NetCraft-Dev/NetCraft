using NetCraft.Codec;

namespace NetCraft.Game.World.Scores;

//ObjectiveCriteria scoring criteria, maps to vanilla net.minecraft.world.scores.criteria.ObjectiveCriteria
//Built-in criteria are registered in static initialization; criteria with a colon look up a statistic type, which is not wired up in this project, so they are unsupported
public sealed class ObjectiveCriteria
{
    //RenderType render type, maps to vanilla RenderType
    public enum RenderType
    {
        INTEGER,
        HEARTS
    }

    //Cache all criteria, maps to vanilla CRITERIA_CACHE
    private static readonly Dictionary<string, ObjectiveCriteria> Cache = new();

    //CustomNames built-in criteria names, maps to vanilla CUSTOM_CRITERIA
    private static readonly HashSet<string> CustomNames = new();

    //Codec persistence codec, keyed by name, maps to vanilla CODEC
    public static readonly Codec<ObjectiveCriteria> Codec = Codecs.String.ComapFlatMap(
        name => ByName(name) is { } criteria
            ? DataResult<ObjectiveCriteria>.Success(criteria)
            : DataResult<ObjectiveCriteria>.Error(() => $"no criteria named {name}"),
        criteria => criteria.Name);

    //RenderTypeCodec render type codec, keyed by serialized name, maps to vanilla RenderType.CODEC
    public static readonly Codec<RenderType> RenderTypeCodec = Codecs.String.ComapFlatMap(
        id => id switch
        {
            "integer" => DataResult<RenderType>.Success(RenderType.INTEGER),
            "hearts" => DataResult<RenderType>.Success(RenderType.HEARTS),
            _ => DataResult<RenderType>.Error(() => $"unknown render type {id}")
        },
        type => type == RenderType.HEARTS ? "hearts" : "integer");

    public static readonly ObjectiveCriteria DUMMY = RegisterCustom("dummy");
    public static readonly ObjectiveCriteria TRIGGER = RegisterCustom("trigger");
    public static readonly ObjectiveCriteria DEATH_COUNT = RegisterCustom("deathCount");
    public static readonly ObjectiveCriteria KILL_COUNT_PLAYERS = RegisterCustom("playerKillCount");
    public static readonly ObjectiveCriteria KILL_COUNT_ALL = RegisterCustom("totalKillCount");
    public static readonly ObjectiveCriteria HEALTH = RegisterCustom("health", true, RenderType.HEARTS);
    public static readonly ObjectiveCriteria FOOD = RegisterCustom("food", true, RenderType.INTEGER);
    public static readonly ObjectiveCriteria AIR = RegisterCustom("air", true, RenderType.INTEGER);
    public static readonly ObjectiveCriteria ARMOR = RegisterCustom("armor", true, RenderType.INTEGER);
    public static readonly ObjectiveCriteria EXPERIENCE = RegisterCustom("xp", true, RenderType.INTEGER);
    public static readonly ObjectiveCriteria LEVEL = RegisterCustom("level", true, RenderType.INTEGER);

    //TEAM_KILL and KILLED_BY_TEAM each have fifteen entries, one per team color, maps to the two vanilla tables of the same names
    public static readonly Dictionary<TeamColor, ObjectiveCriteria> TEAM_KILL =
        RegisterForEveryTeamColor(color => "teamkill." + color.GetSerializedName());

    public static readonly Dictionary<TeamColor, ObjectiveCriteria> KILLED_BY_TEAM =
        RegisterForEveryTeamColor(color => "killedByTeam." + color.GetSerializedName());

    private readonly string _name;
    private readonly bool _readOnly;
    private readonly RenderType _renderType;

    private ObjectiveCriteria(string name, bool readOnly, RenderType renderType)
    {
        _name = name;
        _readOnly = readOnly;
        _renderType = renderType;
        Cache[name] = this;
    }

    //Name criteria name, maps to vanilla getName
    public string Name => _name;

    //IsReadOnly read-only criteria reject score changes from commands, maps to vanilla isReadOnly
    public bool IsReadOnly => _readOnly;

    //DefaultRenderType default render type, maps to vanilla getDefaultRenderType
    public RenderType DefaultRenderType => _renderType;

    //GetCustomCriteriaNames set of built-in criteria names, maps to vanilla getCustomCriteriaNames
    public static IReadOnlyCollection<string> GetCustomCriteriaNames() => CustomNames;

    //ByName looks up criteria by name, null when not found, maps to vanilla byName
    //Vanilla also supports colon-prefixed lookup of statistic types; the statistics system is not wired up so that branch is absent
    public static ObjectiveCriteria? ByName(string name) => Cache.TryGetValue(name, out var criteria) ? criteria : null;

    //RegisterCustom registers a built-in criteria, maps to vanilla registerCustom
    private static ObjectiveCriteria RegisterCustom(string name, bool readOnly, RenderType renderType)
    {
        var criteria = new ObjectiveCriteria(name, readOnly, renderType);
        CustomNames.Add(name);
        return criteria;
    }

    private static ObjectiveCriteria RegisterCustom(string name) => RegisterCustom(name, false, RenderType.INTEGER);

    //RegisterForEveryTeamColor registers one criteria per team color, maps to vanilla registerForEveryTeamColor
    private static Dictionary<TeamColor, ObjectiveCriteria> RegisterForEveryTeamColor(Func<TeamColor, string> idFactory)
    {
        var result = new Dictionary<TeamColor, ObjectiveCriteria>();
        foreach (var color in Enum.GetValues<TeamColor>())
            result[color] = RegisterCustom(idFactory(color));
        return result;
    }
}
