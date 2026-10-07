using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//ScoreHolder a score holder within the scoreboard, maps to vanilla net.minecraft.world.scores.ScoreHolder
//Player entities and plain names can both be holders; the plain-name form is used for commands and fake entries
public interface ScoreHolder
{
    //WildcardName wildcard name, maps to vanilla WILDCARD_NAME
    public const string WildcardName = "*";

    //GetScoreboardName the name used on the scoreboard, maps to vanilla getScoreboardName
    string GetScoreboardName();

    //GetDisplayName display name, null when none, maps to vanilla getDisplayName
    Component? GetDisplayName() => null;

    //Wildcard wildcard holder, maps to vanilla WILDCARD
    public static readonly ScoreHolder Wildcard = new NameOnlyHolder(WildcardName);

    //ForNameOnly constructs from a name only, maps to vanilla forNameOnly
    public static ScoreHolder ForNameOnly(string name) => new NameOnlyHolder(name);
}

//NameOnlyHolder name-only holder, maps to the anonymous implementation of forNameOnly in vanilla
internal sealed class NameOnlyHolder(string name) : ScoreHolder
{
    public string GetScoreboardName() => name;
}
