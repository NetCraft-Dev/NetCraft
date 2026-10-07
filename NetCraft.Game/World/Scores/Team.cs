using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//Team team abstract base class, maps to vanilla net.minecraft.world.scores.Team
//PlayerTeam is its only implementation, the scoreboard reads it through its own view interface
public abstract class Team
{
    //Visibility name tag visibility, maps to vanilla Visibility
    public enum Visibility
    {
        ALWAYS,
        NEVER,
        HIDE_FOR_OTHER_TEAMS,
        HIDE_FOR_OWN_TEAM
    }

    //CollisionRule collision rule, maps to vanilla CollisionRule
    public enum CollisionRule
    {
        ALWAYS,
        NEVER,
        PUSH_OTHER_TEAMS,
        PUSH_OWN_TEAM
    }

    //IsAlliedTo whether they belong to the same team, maps to vanilla isAlliedTo
    public bool IsAlliedTo(Team? other) => other is not null && ReferenceEquals(this, other);

    //GetName team name, maps to vanilla getName
    public abstract string GetName();

    //GetFormattedName colors a piece of text according to the team rules, maps to vanilla getFormattedName
    public abstract Component GetFormattedName(Component component);

    //CanSeeFriendlyInvisibles whether teammates can see invisible allies, maps to vanilla canSeeFriendlyInvisibles
    public abstract bool CanSeeFriendlyInvisibles();

    //IsAllowFriendlyFire whether friendly fire is allowed, maps to vanilla isAllowFriendlyFire
    public abstract bool IsAllowFriendlyFire();

    //GetNameTagVisibility name tag visibility, maps to vanilla getNameTagVisibility
    public abstract Visibility GetNameTagVisibility();

    //GetColor team color, null when none, maps to vanilla getColor
    public abstract TeamColor? GetColor();

    //GetPlayers team member names, maps to vanilla getPlayers
    public abstract IReadOnlyCollection<string> GetPlayers();

    //GetDeathMessageVisibility death message visibility, maps to vanilla getDeathMessageVisibility
    public abstract Visibility GetDeathMessageVisibility();

    //GetCollisionRule collision rule, maps to vanilla getCollisionRule
    public abstract CollisionRule GetCollisionRule();
}

//TeamEnums serialized names and codecs for name tag visibility and collision rule
public static class TeamEnums
{
    //GetName name tag visibility serialized name, maps to vanilla getSerializedName
    public static string GetName(this Team.Visibility visibility) => visibility switch
    {
        Team.Visibility.ALWAYS => "always",
        Team.Visibility.NEVER => "never",
        Team.Visibility.HIDE_FOR_OTHER_TEAMS => "hideForOtherTeams",
        _ => "hideForOwnTeam"
    };

    //GetName collision rule serialized name, maps to vanilla getSerializedName
    public static string GetName(this Team.CollisionRule rule) => rule switch
    {
        Team.CollisionRule.ALWAYS => "always",
        Team.CollisionRule.NEVER => "never",
        Team.CollisionRule.PUSH_OTHER_TEAMS => "pushOtherTeams",
        _ => "pushOwnTeam"
    };

    //VisibilityCodec name tag visibility codec, maps to vanilla Visibility.CODEC
    public static readonly Codec<Team.Visibility> VisibilityCodec = Codecs.String.ComapFlatMap(
        name => name switch
        {
            "always" => DataResult<Team.Visibility>.Success(Team.Visibility.ALWAYS),
            "never" => DataResult<Team.Visibility>.Success(Team.Visibility.NEVER),
            "hideForOtherTeams" => DataResult<Team.Visibility>.Success(Team.Visibility.HIDE_FOR_OTHER_TEAMS),
            "hideForOwnTeam" => DataResult<Team.Visibility>.Success(Team.Visibility.HIDE_FOR_OWN_TEAM),
            _ => DataResult<Team.Visibility>.Error(() => $"unknown name tag visibility {name}")
        },
        visibility => visibility.GetName());

    //CollisionRuleCodec collision rule codec, maps to vanilla CollisionRule.CODEC
    public static readonly Codec<Team.CollisionRule> CollisionRuleCodec = Codecs.String.ComapFlatMap(
        name => name switch
        {
            "always" => DataResult<Team.CollisionRule>.Success(Team.CollisionRule.ALWAYS),
            "never" => DataResult<Team.CollisionRule>.Success(Team.CollisionRule.NEVER),
            "pushOtherTeams" => DataResult<Team.CollisionRule>.Success(Team.CollisionRule.PUSH_OTHER_TEAMS),
            "pushOwnTeam" => DataResult<Team.CollisionRule>.Success(Team.CollisionRule.PUSH_OWN_TEAM),
            _ => DataResult<Team.CollisionRule>.Error(() => $"unknown collision rule {name}")
        },
        rule => rule.GetName());
}
