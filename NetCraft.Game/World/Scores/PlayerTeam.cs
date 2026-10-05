using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//PlayerTeam 玩家队伍 对应原版 net.minecraft.world.scores.PlayerTeam
//持成员名单前后缀显示名可见性与碰撞规则 每次改动通知计分板
public sealed class PlayerTeam : Team
{
    //_players 成员名单 对应原版 players
    private readonly HashSet<string> _players = new();

    private Component _displayName;
    private Component _playerPrefix = CommonComponents.Empty;
    private Component _playerSuffix = CommonComponents.Empty;
    private bool _allowFriendlyFire = true;
    private bool _seeFriendlyInvisibles = true;
    private Team.Visibility _nameTagVisibility = Team.Visibility.ALWAYS;
    private Team.Visibility _deathMessageVisibility = Team.Visibility.ALWAYS;
    private TeamColor? _color;
    private Team.CollisionRule _collisionRule = Team.CollisionRule.ALWAYS;

    public PlayerTeam(Scoreboard scoreboard, string name)
    {
        Scoreboard = scoreboard;
        Name = name;
        _displayName = Component.Literal(name);
    }

    //Scoreboard 所属计分板 对应原版 getScoreboard
    public Scoreboard Scoreboard { get; }

    //Name 队伍名
    public string Name { get; }

    public override string GetName() => Name;

    //DisplayName 显示名 对应原版 getDisplayName
    public Component DisplayName => _displayName;

    //SetDisplayName 改显示名并通知计分板 对应原版 setDisplayName
    public void SetDisplayName(Component displayName)
    {
        _displayName = displayName;
        Scoreboard.OnTeamChanged(this);
    }

    //PlayerPrefix 成员名前缀 对应原版 getPlayerPrefix
    public Component PlayerPrefix => _playerPrefix;

    //SetPlayerPrefix 改成员名前缀并通知计分板 对应原版 setPlayerPrefix
    public void SetPlayerPrefix(Component? prefix)
    {
        _playerPrefix = prefix ?? CommonComponents.Empty;
        Scoreboard.OnTeamChanged(this);
    }

    //PlayerSuffix 成员名后缀 对应原版 getPlayerSuffix
    public Component PlayerSuffix => _playerSuffix;

    //SetPlayerSuffix 改成员名后缀并通知计分板 对应原版 setPlayerSuffix
    public void SetPlayerSuffix(Component? suffix)
    {
        _playerSuffix = suffix ?? CommonComponents.Empty;
        Scoreboard.OnTeamChanged(this);
    }

    //GetFormattedName 给名字套上前后缀与队伍颜色 对应原版 getFormattedName
    public override Component GetFormattedName(Component component)
    {
        var result = Component.Empty().Append(_playerPrefix).Append(component).Append(_playerSuffix);
        if (_color is { } color) result.WithColor(color.GetTextColor());
        return result;
    }

    //IsAllowFriendlyFire 是否允许友伤 对应原版 isAllowFriendlyFire
    public override bool IsAllowFriendlyFire() => _allowFriendlyFire;

    //SetAllowFriendlyFire 设置友伤并通知计分板 对应原版 setAllowFriendlyFire
    public void SetAllowFriendlyFire(bool allowFriendlyFire)
    {
        _allowFriendlyFire = allowFriendlyFire;
        Scoreboard.OnTeamChanged(this);
    }

    //CanSeeFriendlyInvisibles 能否看见同队隐身单位 对应原版 canSeeFriendlyInvisibles
    public override bool CanSeeFriendlyInvisibles() => _seeFriendlyInvisibles;

    //SetSeeFriendlyInvisibles 设置并通知计分板 对应原版 setSeeFriendlyInvisibles
    public void SetSeeFriendlyInvisibles(bool seeFriendlyInvisibles)
    {
        _seeFriendlyInvisibles = seeFriendlyInvisibles;
        Scoreboard.OnTeamChanged(this);
    }

    public override Team.Visibility GetNameTagVisibility() => _nameTagVisibility;

    //SetNameTagVisibility 设置名牌可见性并通知计分板 对应原版 setNameTagVisibility
    public void SetNameTagVisibility(Team.Visibility visibility)
    {
        _nameTagVisibility = visibility;
        Scoreboard.OnTeamChanged(this);
    }

    public override Team.Visibility GetDeathMessageVisibility() => _deathMessageVisibility;

    //SetDeathMessageVisibility 设置死亡消息可见性并通知计分板 对应原版 setDeathMessageVisibility
    public void SetDeathMessageVisibility(Team.Visibility visibility)
    {
        _deathMessageVisibility = visibility;
        Scoreboard.OnTeamChanged(this);
    }

    public override Team.CollisionRule GetCollisionRule() => _collisionRule;

    //SetCollisionRule 设置碰撞规则并通知计分板 对应原版 setCollisionRule
    public void SetCollisionRule(Team.CollisionRule collisionRule)
    {
        _collisionRule = collisionRule;
        Scoreboard.OnTeamChanged(this);
    }

    public override TeamColor? GetColor() => _color;

    //SetColor 设置队伍颜色并通知计分板 对应原版 setColor
    public void SetColor(TeamColor? color)
    {
        _color = color;
        Scoreboard.OnTeamChanged(this);
    }

    public override IReadOnlyCollection<string> GetPlayers() => _players;

    //AddPlayer 加成员 对应原版 getPlayers().add 那条路径
    internal bool AddPlayer(string player) => _players.Add(player);

    //RemovePlayer 移出成员 对应原版 getPlayers().remove 那条路径
    internal bool RemovePlayer(string player) => _players.Remove(player);

    //Packed 队伍的存档形态 对应原版 PlayerTeam.Packed
    public sealed record Packed(
        string Name,
        Optional<Component> DisplayName,
        Optional<TeamColor> Color,
        bool AllowFriendlyFire,
        bool SeeFriendlyInvisibles,
        Component MemberNamePrefix,
        Component MemberNameSuffix,
        Team.Visibility NameTagVisibility,
        Team.Visibility DeathMessageVisibility,
        Team.CollisionRule CollisionRule,
        IReadOnlyList<string> Players)
    {
        //Codec 持久化编解码 字段名对齐原版 CODEC
        public static readonly Codec<Packed> Codec = RecordCodecBuilder.Of11(
            Codecs.String.FieldOf("Name").ForGetter((Packed packed) => packed.Name),
            ComponentSerialization.Codec.OptionalFieldOf("DisplayName")
                .ForGetter((Packed packed) => packed.DisplayName),
            TeamColorExtensions.Codec.OptionalFieldOf("TeamColor").ForGetter((Packed packed) => packed.Color),
            Codecs.Bool.FieldOf("AllowFriendlyFire").ForGetter((Packed packed) => packed.AllowFriendlyFire),
            Codecs.Bool.FieldOf("SeeFriendlyInvisibles").ForGetter((Packed packed) => packed.SeeFriendlyInvisibles),
            ComponentSerialization.Codec.FieldOf("MemberNamePrefix")
                .ForGetter((Packed packed) => packed.MemberNamePrefix),
            ComponentSerialization.Codec.FieldOf("MemberNameSuffix")
                .ForGetter((Packed packed) => packed.MemberNameSuffix),
            TeamEnums.VisibilityCodec.FieldOf("NameTagVisibility")
                .ForGetter((Packed packed) => packed.NameTagVisibility),
            TeamEnums.VisibilityCodec.FieldOf("DeathMessageVisibility")
                .ForGetter((Packed packed) => packed.DeathMessageVisibility),
            TeamEnums.CollisionRuleCodec.FieldOf("CollisionRule")
                .ForGetter((Packed packed) => packed.CollisionRule),
            Codecs.String.ListOf().FieldOf("Players").ForGetter((Packed packed) => packed.Players),
            (name, displayName, color, allowFriendlyFire, seeFriendlyInvisibles, prefix, suffix, nameTagVisibility,
                deathMessageVisibility, collisionRule, players) =>
                new Packed(name, displayName, color, allowFriendlyFire, seeFriendlyInvisibles, prefix, suffix,
                    nameTagVisibility, deathMessageVisibility, collisionRule, players));
    }
}
