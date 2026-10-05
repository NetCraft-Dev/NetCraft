using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//Team 队伍抽象基类 对应原版 net.minecraft.world.scores.Team
//玩家队伍是它的唯一实现 计分板另用自己的视图接口读它
public abstract class Team
{
    //Visibility 名牌可见性 对应原版 Visibility
    public enum Visibility
    {
        ALWAYS,
        NEVER,
        HIDE_FOR_OTHER_TEAMS,
        HIDE_FOR_OWN_TEAM
    }

    //CollisionRule 碰撞规则 对应原版 CollisionRule
    public enum CollisionRule
    {
        ALWAYS,
        NEVER,
        PUSH_OTHER_TEAMS,
        PUSH_OWN_TEAM
    }

    //IsAlliedTo 是否属于同一队 对应原版 isAlliedTo
    public bool IsAlliedTo(Team? other) => other is not null && ReferenceEquals(this, other);

    //GetName 队伍名 对应原版 getName
    public abstract string GetName();

    //GetFormattedName 按队伍规则给一段文本着色 对应原版 getFormattedName
    public abstract Component GetFormattedName(Component component);

    //CanSeeFriendlyInvisibles 能否看见同队隐身单位 对应原版 canSeeFriendlyInvisibles
    public abstract bool CanSeeFriendlyInvisibles();

    //IsAllowFriendlyFire 是否允许友伤 对应原版 isAllowFriendlyFire
    public abstract bool IsAllowFriendlyFire();

    //GetNameTagVisibility 名牌可见性 对应原版 getNameTagVisibility
    public abstract Visibility GetNameTagVisibility();

    //GetColor 队伍颜色 没有给 null 对应原版 getColor
    public abstract TeamColor? GetColor();

    //GetPlayers 队伍成员名 对应原版 getPlayers
    public abstract IReadOnlyCollection<string> GetPlayers();

    //GetDeathMessageVisibility 死亡消息可见性 对应原版 getDeathMessageVisibility
    public abstract Visibility GetDeathMessageVisibility();

    //GetCollisionRule 碰撞规则 对应原版 getCollisionRule
    public abstract CollisionRule GetCollisionRule();
}

//TeamEnums 名牌可见性与碰撞规则的序列化名与编解码
public static class TeamEnums
{
    //GetName 名牌可见性序列化名 对应原版 getSerializedName
    public static string GetName(this Team.Visibility visibility) => visibility switch
    {
        Team.Visibility.ALWAYS => "always",
        Team.Visibility.NEVER => "never",
        Team.Visibility.HIDE_FOR_OTHER_TEAMS => "hideForOtherTeams",
        _ => "hideForOwnTeam"
    };

    //GetName 碰撞规则序列化名 对应原版 getSerializedName
    public static string GetName(this Team.CollisionRule rule) => rule switch
    {
        Team.CollisionRule.ALWAYS => "always",
        Team.CollisionRule.NEVER => "never",
        Team.CollisionRule.PUSH_OTHER_TEAMS => "pushOtherTeams",
        _ => "pushOwnTeam"
    };

    //VisibilityCodec 名牌可见性编解码 对应原版 Visibility.CODEC
    public static readonly Codec<Team.Visibility> VisibilityCodec = Codecs.String.ComapFlatMap(
        name => name switch
        {
            "always" => DataResult<Team.Visibility>.Success(Team.Visibility.ALWAYS),
            "never" => DataResult<Team.Visibility>.Success(Team.Visibility.NEVER),
            "hideForOtherTeams" => DataResult<Team.Visibility>.Success(Team.Visibility.HIDE_FOR_OTHER_TEAMS),
            "hideForOwnTeam" => DataResult<Team.Visibility>.Success(Team.Visibility.HIDE_FOR_OWN_TEAM),
            _ => DataResult<Team.Visibility>.Error(() => $"未知名牌可见性 {name}")
        },
        visibility => visibility.GetName());

    //CollisionRuleCodec 碰撞规则编解码 对应原版 CollisionRule.CODEC
    public static readonly Codec<Team.CollisionRule> CollisionRuleCodec = Codecs.String.ComapFlatMap(
        name => name switch
        {
            "always" => DataResult<Team.CollisionRule>.Success(Team.CollisionRule.ALWAYS),
            "never" => DataResult<Team.CollisionRule>.Success(Team.CollisionRule.NEVER),
            "pushOtherTeams" => DataResult<Team.CollisionRule>.Success(Team.CollisionRule.PUSH_OTHER_TEAMS),
            "pushOwnTeam" => DataResult<Team.CollisionRule>.Success(Team.CollisionRule.PUSH_OWN_TEAM),
            _ => DataResult<Team.CollisionRule>.Error(() => $"未知碰撞规则 {name}")
        },
        rule => rule.GetName());
}
