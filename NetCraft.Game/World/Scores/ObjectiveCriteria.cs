using NetCraft.Codec;

namespace NetCraft.Game.World.Scores;

//ObjectiveCriteria 计分标准 对应原版 net.minecraft.world.scores.criteria.ObjectiveCriteria
//内置标准在静态初始化里登记 带冒号的标准要查统计类型 本项目统计体系未接通 暂不支持
public sealed class ObjectiveCriteria
{
    //RenderType 渲染类型 对应原版 RenderType
    public enum RenderType
    {
        INTEGER,
        HEARTS
    }

    //Cache 全部标准 对应原版 CRITERIA_CACHE
    private static readonly Dictionary<string, ObjectiveCriteria> Cache = new();

    //CustomNames 内置标准名 对应原版 CUSTOM_CRITERIA
    private static readonly HashSet<string> CustomNames = new();

    //Codec 持久化编解码 按名字 对应原版 CODEC
    public static readonly Codec<ObjectiveCriteria> Codec = Codecs.String.ComapFlatMap(
        name => ByName(name) is { } criteria
            ? DataResult<ObjectiveCriteria>.Success(criteria)
            : DataResult<ObjectiveCriteria>.Error(() => $"没有名为 {name} 的计分标准"),
        criteria => criteria.Name);

    //RenderTypeCodec 渲染类型编解码 按序列化名 对应原版 RenderType.CODEC
    public static readonly Codec<RenderType> RenderTypeCodec = Codecs.String.ComapFlatMap(
        id => id switch
        {
            "integer" => DataResult<RenderType>.Success(RenderType.INTEGER),
            "hearts" => DataResult<RenderType>.Success(RenderType.HEARTS),
            _ => DataResult<RenderType>.Error(() => $"未知渲染类型 {id}")
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

    //TEAM_KILL 与 KILLED_BY_TEAM 按队伍颜色各十五个 对应原版同名两张表
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

    //Name 标准名 对应原版 getName
    public string Name => _name;

    //IsReadOnly 只读标准不接受命令改分 对应原版 isReadOnly
    public bool IsReadOnly => _readOnly;

    //DefaultRenderType 默认渲染类型 对应原版 getDefaultRenderType
    public RenderType DefaultRenderType => _renderType;

    //GetCustomCriteriaNames 内置标准名集合 对应原版 getCustomCriteriaNames
    public static IReadOnlyCollection<string> GetCustomCriteriaNames() => CustomNames;

    //ByName 按名字查标准 找不到给 null 对应原版 byName
    //原版还支持冒号形式查统计类型 统计体系未接通故暂无该分支
    public static ObjectiveCriteria? ByName(string name) => Cache.TryGetValue(name, out var criteria) ? criteria : null;

    //RegisterCustom 登记一个内置标准 对应原版 registerCustom
    private static ObjectiveCriteria RegisterCustom(string name, bool readOnly, RenderType renderType)
    {
        var criteria = new ObjectiveCriteria(name, readOnly, renderType);
        CustomNames.Add(name);
        return criteria;
    }

    private static ObjectiveCriteria RegisterCustom(string name) => RegisterCustom(name, false, RenderType.INTEGER);

    //RegisterForEveryTeamColor 按队伍颜色各登记一个标准 对应原版 registerForEveryTeamColor
    private static Dictionary<TeamColor, ObjectiveCriteria> RegisterForEveryTeamColor(Func<TeamColor, string> idFactory)
    {
        var result = new Dictionary<TeamColor, ObjectiveCriteria>();
        foreach (var color in Enum.GetValues<TeamColor>())
            result[color] = RegisterCustom(idFactory(color));
        return result;
    }
}
