using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//CombiningPredicate predicate combination base, maps to vanilla CombiningPredicate
//Common base holding a set of sub-predicates, reused by all_of and any_of
public abstract class CombiningPredicate : BlockPredicate
{
    protected readonly IReadOnlyList<BlockPredicate> Predicates;

    protected CombiningPredicate(IReadOnlyList<BlockPredicate> predicates) => Predicates = predicates;

    //CreateCodec build the codec for the predicates array field, maps to vanilla codec(constructor)
    public static Codec<T> CreateCodec<T>(Func<IReadOnlyList<BlockPredicate>, T> constructor)
        where T : CombiningPredicate
        => new SingleFieldMapCodec<T, IReadOnlyList<BlockPredicate>>(
            BlockPredicate.Codec.ListOf().FieldOf("predicates"), constructor, p => p.Predicates);
}
