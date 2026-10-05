using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.Advancements.Predicates;

//CollectionCountsPredicate 集合数量谓词 对应原版 CollectionCountsPredicate
//语义是"满足某项的元素个数落在给定区间" 按项数退化成 Zero/Single/Multiple
//单项要求该项计数命中区间 多项要求每项各自计数都命中
public interface CollectionCountsPredicate<T, P> where P : class, IValuePredicate<T>
{
    //Unpack 展开回项列表 编解码反向用
    IReadOnlyList<Entry> Unpack();

    //Test 集合是否满足
    bool Test(IEnumerable<T> values);

    //Codec 持久化编解码就是项列表 对应原版 codec
    public static Codec<CollectionCountsPredicate<T, P>> Codec(Codec<P> elementCodec)
        => Entry.Codec(elementCodec).ListOf().ComapFlatMap(
            list => DataResult<CollectionCountsPredicate<T, P>>.Success(Of(list)),
            predicate => predicate.Unpack());

    //Of 按项数选实现 对应原版 of
    public static CollectionCountsPredicate<T, P> Of(IReadOnlyList<Entry> predicates) => predicates.Count switch
    {
        0 => new Zero(),
        1 => new Single(predicates[0]),
        _ => new Multiple(predicates)
    };

    //Entry 一项 判定对象与计数区间 对应原版 Entry
    public sealed record Entry(P Predicate, MinMaxBounds.Ints Count)
    {
        //Codec 项编解码 test 与 count 两个字段
        public static Codec<Entry> Codec(Codec<P> elementCodec) => RecordCodecBuilder.Of2(
            elementCodec.FieldOf("test").ForGetter((Entry entry) => entry.Predicate),
            MinMaxBounds.Ints.CODEC.FieldOf(ItemInstance.FieldCount).ForGetter((Entry entry) => entry.Count),
            (predicate, count) => new Entry(predicate, count));

        //Matches 计数满足该项的元素个数是否落在区间
        public bool Matches(IEnumerable<T> values)
        {
            var count = 0;
            foreach (var value in values)
                if (Predicate.Test(value)) count++;
            return Count.Matches(count);
        }
    }

    //Zero 空项列表恒真
    public sealed class Zero : CollectionCountsPredicate<T, P>
    {
        public IReadOnlyList<Entry> Unpack() => Array.Empty<Entry>();

        public bool Test(IEnumerable<T> values) => true;
    }

    //Single 单项
    public sealed class Single(Entry entry) : CollectionCountsPredicate<T, P>
    {
        public Entry Value { get; } = entry;

        public IReadOnlyList<Entry> Unpack() => new[] { Value };

        public bool Test(IEnumerable<T> values) => Value.Matches(values);
    }

    //Multiple 多项 每项都要成立
    public sealed class Multiple(IReadOnlyList<Entry> entries) : CollectionCountsPredicate<T, P>
    {
        public IReadOnlyList<Entry> Entries { get; } = entries;

        public IReadOnlyList<Entry> Unpack() => Entries;

        public bool Test(IEnumerable<T> values)
        {
            var list = values as IReadOnlyList<T> ?? values.ToList();
            foreach (var entry in Entries)
                if (!entry.Matches(list)) return false;
            return true;
        }
    }
}
