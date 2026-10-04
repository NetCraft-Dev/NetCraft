namespace NetCraft.Game.World.Level;

//GameType 游戏模式对标原版 net.minecraft.world.level.GameType
//静态实例模式替代 enum 携带 id/name/shortName 与能力标志
//id 与网络协议/Player.GameMode int 一致 0=生存 1=创造 2=冒险 3=旁观
public sealed class GameType
{
    //Survival 生存 可构建可破坏
    public static readonly GameType Survival = new(0, "survival", "s");
    //Creative 创造 可飞行 instabuild
    public static readonly GameType Creative = new(1, "creative", "c");
    //Adventure 冒险 限制放置方块
    public static readonly GameType Adventure = new(2, "adventure", "a");
    //Spectator 旁观 无碰撞可飞行不可交互
    public static readonly GameType Spectator = new(3, "spectator", "sp");

    //All 全部模式按 id 升序供遍历
    private static readonly GameType[] s_all = { Survival, Creative, Adventure, Spectator };

    //Id 数字 id 与协议一致
    public int Id { get; }
    //Name 全名如 survival
    public string Name { get; }
    //ShortName 短名如 s
    public string ShortName { get; }

    private GameType(int id, string name, string shortName)
    {
        Id = id;
        Name = name;
        ShortName = shortName;
    }

    //IsCreative 创造或旁观 原版 isCreative 语义
    public bool IsCreative => this == Creative || this == Spectator;
    //IsSurvival 生存或冒险 原版 isSurvival 语义
    public bool IsSurvival => this == Survival || this == Adventure;
    //IsBlockPlacingRestricted 冒险或旁观限制放置/破坏 原版 isBlockPlacingRestricted 语义
    public bool IsBlockPlacingRestricted => this == Adventure || this == Spectator;
    //IsFlyAllowed 创造或旁观可飞行 原版 isFlyAllowed 语义
    public bool IsFlyAllowed => this == Creative || this == Spectator;

    //ById 按数字 id 查模式 未找到返回 null
    public static GameType? ById(int id)
    {
        foreach (var type in s_all)
            if (type.Id == id) return type;
        return null;
    }

    //ByName 按名称或短名解析忽略大小写 未找到返回 null
    //原版 byName 遍历匹配 name/shortName equalsIgnoreCase
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
