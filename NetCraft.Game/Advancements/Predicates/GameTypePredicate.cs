using NetCraft.Codec;
using NetCraft.Game.World.Level;

namespace NetCraft.Game.Advancements.Predicates;

//GameTypePredicate game type predicate, checks whether the mode falls in the given set
//maps to vanilla net.minecraft.advancements.predicates.GameTypePredicate
public sealed record GameTypePredicate(IReadOnlyList<GameType> Types)
{
    //Any all modes, maps to vanilla ANY
    public static readonly GameTypePredicate Any =
        Of(GameType.Survival, GameType.Creative, GameType.Adventure, GameType.Spectator);

    //SurvivalLike survival and adventure, maps to vanilla SURVIVAL_LIKE
    public static readonly GameTypePredicate SurvivalLike = Of(GameType.Survival, GameType.Adventure);

    //Codec mode name list, maps to vanilla CODEC
    public static readonly Codec<GameTypePredicate> Codec = GameType.Codec.ListOf().ComapFlatMap(
        types => DataResult<GameTypePredicate>.Success(new GameTypePredicate(types)),
        predicate => predicate.Types);

    //Of builds from the given modes, maps to vanilla of
    public static GameTypePredicate Of(params GameType[] types) => new(types);

    //Matches whether the mode is in the set, maps to vanilla matches
    public bool Matches(GameType type) => Types.Contains(type);
}
