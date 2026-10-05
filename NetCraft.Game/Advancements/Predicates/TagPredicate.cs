using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//TagPredicate 标签谓词 判定引用是否属于某标签 可要求属于或不属于
//对应原版 net.minecraft.advancements.predicates.TagPredicate
public sealed record TagPredicate<T>(TagKey<T> Tag, bool Expected) where T : class
{
    //Codec 持久化编解码 字段名 id 与 expected 对应原版 codec
    public static Codec<TagPredicate<T>> Codec(ResourceKey<Registry<T>> registryKey) => RecordCodecBuilder.Of2(
        TagKey<T>.Codec(registryKey).FieldOf("id").ForGetter((TagPredicate<T> predicate) => predicate.Tag),
        Codecs.Bool.FieldOf("expected").ForGetter((TagPredicate<T> predicate) => predicate.Expected),
        (tag, expected) => new TagPredicate<T>(tag, expected));

    //Is 要求引用属于该标签 对应原版 is
    public static TagPredicate<T> Is(TagKey<T> tag) => new(tag, true);

    //IsNot 要求引用不属于该标签 对应原版 isNot
    public static TagPredicate<T> IsNot(TagKey<T> tag) => new(tag, false);

    //Matches 归属判定结果与期望一致 对应原版 matches
    public bool Matches(Holder<T> holder) => holder.Is(Tag) == Expected;
}
