using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//ScoreHolder 计分板里的记分对象 对应原版 net.minecraft.world.scores.ScoreHolder
//玩家实体与纯名字都能当持有者 纯名字那条用于命令与假名条目
public interface ScoreHolder
{
    //WildcardName 通配名 对应原版 WILDCARD_NAME
    public const string WildcardName = "*";

    //GetScoreboardName 计分板里用的名字 对应原版 getScoreboardName
    string GetScoreboardName();

    //GetDisplayName 显示名 没有给 null 对应原版 getDisplayName
    Component? GetDisplayName() => null;

    //Wildcard 通配持有者 对应原版 WILDCARD
    public static readonly ScoreHolder Wildcard = new NameOnlyHolder(WildcardName);

    //ForNameOnly 只按名字构造 对应原版 forNameOnly
    public static ScoreHolder ForNameOnly(string name) => new NameOnlyHolder(name);
}

//NameOnlyHolder 只有名字的持有者 对应原版里 forNameOnly 的匿名实现
internal sealed class NameOnlyHolder(string name) : ScoreHolder
{
    public string GetScoreboardName() => name;
}
