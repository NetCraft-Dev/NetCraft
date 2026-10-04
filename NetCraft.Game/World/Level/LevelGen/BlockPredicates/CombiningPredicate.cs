using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//CombiningPredicate 谓词组合基类对应原版 CombiningPredicate
//装载一组子谓词的公共基类 供 all_of 与 any_of 复用
public abstract class CombiningPredicate : BlockPredicate
{
    protected readonly IReadOnlyList<BlockPredicate> Predicates;

    protected CombiningPredicate(IReadOnlyList<BlockPredicate> predicates) => Predicates = predicates;

    //CreateCodec 构造 predicates 数组字段的 codec 对应原版 codec(constructor)
    public static Codec<T> CreateCodec<T>(Func<IReadOnlyList<BlockPredicate>, T> constructor)
        where T : CombiningPredicate
        => new SingleFieldMapCodec<T, IReadOnlyList<BlockPredicate>>(
            BlockPredicate.Codec.ListOf().FieldOf("predicates"), constructor, p => p.Predicates);
}
