using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//PlayerTeam player team, maps to vanilla net.minecraft.world.scores.PlayerTeam
//Holds the member list, prefix/suffix, display name, visibility and collision rules; every change notifies the scoreboard
public sealed class PlayerTeam : Team
{
    //_players member list, maps to vanilla players
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

    //Scoreboard owning scoreboard, maps to vanilla getScoreboard
    public Scoreboard Scoreboard { get; }

    //Name team name
    public string Name { get; }

    public override string GetName() => Name;

    //DisplayName display name, maps to vanilla getDisplayName
    public Component DisplayName => _displayName;

    //SetDisplayName changes the display name and notifies the scoreboard, maps to vanilla setDisplayName
    public void SetDisplayName(Component displayName)
    {
        _displayName = displayName;
        Scoreboard.OnTeamChanged(this);
    }

    //PlayerPrefix member name prefix, maps to vanilla getPlayerPrefix
    public Component PlayerPrefix => _playerPrefix;

    //SetPlayerPrefix changes the member name prefix and notifies the scoreboard, maps to vanilla setPlayerPrefix
    public void SetPlayerPrefix(Component? prefix)
    {
        _playerPrefix = prefix ?? CommonComponents.Empty;
        Scoreboard.OnTeamChanged(this);
    }

    //PlayerSuffix member name suffix, maps to vanilla getPlayerSuffix
    public Component PlayerSuffix => _playerSuffix;

    //SetPlayerSuffix changes the member name suffix and notifies the scoreboard, maps to vanilla setPlayerSuffix
    public void SetPlayerSuffix(Component? suffix)
    {
        _playerSuffix = suffix ?? CommonComponents.Empty;
        Scoreboard.OnTeamChanged(this);
    }

    //GetFormattedName wraps a name with the prefix, suffix and team color, maps to vanilla getFormattedName
    public override Component GetFormattedName(Component component)
    {
        var result = Component.Empty().Append(_playerPrefix).Append(component).Append(_playerSuffix);
        if (_color is { } color) result.WithColor(color.GetTextColor());
        return result;
    }

    //IsAllowFriendlyFire whether friendly fire is allowed, maps to vanilla isAllowFriendlyFire
    public override bool IsAllowFriendlyFire() => _allowFriendlyFire;

    //SetAllowFriendlyFire sets friendly fire and notifies the scoreboard, maps to vanilla setAllowFriendlyFire
    public void SetAllowFriendlyFire(bool allowFriendlyFire)
    {
        _allowFriendlyFire = allowFriendlyFire;
        Scoreboard.OnTeamChanged(this);
    }

    //CanSeeFriendlyInvisibles whether teammates can see invisible allies, maps to vanilla canSeeFriendlyInvisibles
    public override bool CanSeeFriendlyInvisibles() => _seeFriendlyInvisibles;

    //SetSeeFriendlyInvisibles sets it and notifies the scoreboard, maps to vanilla setSeeFriendlyInvisibles
    public void SetSeeFriendlyInvisibles(bool seeFriendlyInvisibles)
    {
        _seeFriendlyInvisibles = seeFriendlyInvisibles;
        Scoreboard.OnTeamChanged(this);
    }

    public override Team.Visibility GetNameTagVisibility() => _nameTagVisibility;

    //SetNameTagVisibility sets name tag visibility and notifies the scoreboard, maps to vanilla setNameTagVisibility
    public void SetNameTagVisibility(Team.Visibility visibility)
    {
        _nameTagVisibility = visibility;
        Scoreboard.OnTeamChanged(this);
    }

    public override Team.Visibility GetDeathMessageVisibility() => _deathMessageVisibility;

    //SetDeathMessageVisibility sets death message visibility and notifies the scoreboard, maps to vanilla setDeathMessageVisibility
    public void SetDeathMessageVisibility(Team.Visibility visibility)
    {
        _deathMessageVisibility = visibility;
        Scoreboard.OnTeamChanged(this);
    }

    public override Team.CollisionRule GetCollisionRule() => _collisionRule;

    //SetCollisionRule sets the collision rule and notifies the scoreboard, maps to vanilla setCollisionRule
    public void SetCollisionRule(Team.CollisionRule collisionRule)
    {
        _collisionRule = collisionRule;
        Scoreboard.OnTeamChanged(this);
    }

    public override TeamColor? GetColor() => _color;

    //SetColor sets the team color and notifies the scoreboard, maps to vanilla setColor
    public void SetColor(TeamColor? color)
    {
        _color = color;
        Scoreboard.OnTeamChanged(this);
    }

    public override IReadOnlyCollection<string> GetPlayers() => _players;

    //AddPlayer adds a member, maps to the getPlayers().add path in vanilla
    internal bool AddPlayer(string player) => _players.Add(player);

    //RemovePlayer removes a member, maps to the getPlayers().remove path in vanilla
    internal bool RemovePlayer(string player) => _players.Remove(player);

    //Packed save form of the team, maps to vanilla PlayerTeam.Packed
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
        //Codec persistence codec, field names align with vanilla CODEC
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
