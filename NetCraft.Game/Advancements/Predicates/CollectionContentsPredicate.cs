using NetCraft.Codec;

namespace NetCraft.Game.Advancements.Predicates;

//CollectionContentsPredicate 集合内容谓词 对应原版 CollectionContentsPredicate
//语义是集合里存在元素满足某项 按项数退化成 Zero/Single/Multiple
//单项是"存在一个元素满足" 多项是"每项都能被不同元素各命中一次"
public interface CollectionContentsPredicate<T, P> where P : class, IValuePredicate<T>
{
    //Unpack 展开回项列表 编解码反向用
    IReadOnlyList<P> Unpack();

    //Test 集合是否满足
    bool Test(IEnumerable<T> values);

    //Codec 持久化编解码就是项列表 对应原版 codec
    public static Codec<CollectionContentsPredicate<T, P>> Codec(Codec<P> elementCodec)
        => elementCodec.ListOf().ComapFlatMap(
            list => DataResult<CollectionContentsPredicate<T, P>>.Success(Of(list)),
            predicate => predicate.Unpack());

    //Of 按项数选实现 对应原版 of
    public static CollectionContentsPredicate<T, P> Of(IReadOnlyList<P> predicates) => predicates.Count switch
    {
        0 => new Zero(),
        1 => new Single(predicates[0]),
        _ => new Multiple(predicates)
    };

    //Zero 空项列表恒真
    public sealed class Zero : CollectionContentsPredicate<T, P>
    {
        public IReadOnlyList<P> Unpack() => Array.Empty<P>();

        public bool Test(IEnumerable<T> values) => true;
    }

    //Single 单项 集合里存在元素满足它
    public sealed class Single(P predicate) : CollectionContentsPredicate<T, P>
    {
        public P Predicate { get; } = predicate;

        public IReadOnlyList<P> Unpack() => new[] { Predicate };

        public bool Test(IEnumerable<T> values) => values.Any(Predicate.Test);
    }

    //Multiple 多项 每项都要被不同元素命中一遍
    public sealed class Multiple(IReadOnlyList<P> tests) : CollectionContentsPredicate<T, P>
    {
        public IReadOnlyList<P> Tests { get; } = tests;

        public IReadOnlyList<P> Unpack() => Tests;

        public bool Test(IEnumerable<T> values)
        {
            var remaining = new List<P>(Tests);
            foreach (var value in values)
            {
                remaining.RemoveAll(item => item.Test(value));
                if (remaining.Count == 0) return true;
            }
            return false;
        }
    }
}
