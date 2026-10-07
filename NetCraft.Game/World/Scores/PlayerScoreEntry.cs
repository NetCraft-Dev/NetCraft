using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//PlayerScoreEntry scoreboard entry snapshot, maps to vanilla net.minecraft.world.scores.PlayerScoreEntry
//Used for display and list queries; the vanilla number format override field is not wired up and is omitted
public sealed record PlayerScoreEntry(string Owner, int Value, Component? Display)
{
    //IsHidden entries starting with a hash are hidden from players, maps to vanilla isHidden
    public bool IsHidden => Owner.StartsWith('#');

    //OwnerName the name with the hidden prefix stripped, maps to vanilla ownerName
    public string OwnerName() => IsHidden ? Owner[1..] : Owner;
}
