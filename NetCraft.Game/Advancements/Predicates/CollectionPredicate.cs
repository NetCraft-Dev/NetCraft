using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.Advancements.Predicates;

//CollectionPredicate 集合谓词 大小区间加内容谓词加数量谓词 对应原版 net.minecraft.advancements.predicates.CollectionPredicate
//三项都是可选的 缺省即不约束
public sealed record CollectionPredicate<T, P>(
    Optional<CollectionContentsPredicate<T, P>> Contains,
    Optional<CollectionCountsPredicate<T, P>> Counts,
    Optional<MinMaxBounds.Ints> Size) where P : class, IValuePredicate<T>
{
    //Codec 持久化编解码 字段名 contains 与 count 与 size 对应原版 codec
    public static Codec<CollectionPredicate<T, P>> Codec(Codec<P> elementCodec) => RecordCodecBuilder.Of3(
        CollectionContentsPredicate<T, P>.Codec(elementCodec).OptionalFieldOf("contains")
            .ForGetter((CollectionPredicate<T, P> predicate) => predicate.Contains),
        CollectionCountsPredicate<T, P>.Codec(elementCodec).OptionalFieldOf(ItemInstance.FieldCount)
            .ForGetter((CollectionPredicate<T, P> predicate) => predicate.Counts),
        MinMaxBounds.Ints.CODEC.OptionalFieldOf("size")
            .ForGetter((CollectionPredicate<T, P> predicate) => predicate.Size),
        (contains, counts, size) => new CollectionPredicate<T, P>(contains, counts, size));

    //Test 集合是否同时满足大小与内容与数量约束
    public bool Test(IReadOnlyList<T> items)
    {
        if (Size.IsPresent && !Size.Get().Matches(items.Count)) return false;
        if (Contains.IsPresent && !Contains.Get().Test(items)) return false;
        if (Counts.IsPresent && !Counts.Get().Test(items)) return false;
        return true;
    }
}
