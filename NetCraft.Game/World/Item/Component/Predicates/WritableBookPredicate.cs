using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Network;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//WritableBookPredicate 书与笔谓词 判定页集合是否满足集合谓词
//对应原版 net.minecraft.core.component.predicates.WritableBookPredicate
public sealed record WritableBookPredicate(
    Optional<CollectionPredicate<Filterable<string>, WritableBookPredicate.PagePredicate>> Pages)
    : SingleComponentItemPredicate<WritableBookContent>
{
    //Codec 持久化编解码 只有 pages 一个字段 对应原版 CODEC
    public static readonly Codec<WritableBookPredicate> Codec = RecordCodecBuilder.Of1(
        CollectionPredicate<Filterable<string>, PagePredicate>.Codec(PagePredicate.Codec)
            .OptionalFieldOf("pages")
            .ForGetter((WritableBookPredicate predicate) => predicate.Pages),
        pages => new WritableBookPredicate(pages));

    public DataComponentType<object> ComponentType => DataComponents.WRITABLE_BOOK_CONTENT;

    public bool MatchesValue(WritableBookContent value) => !Pages.IsPresent || Pages.Get().Test(value.Pages);

    //PagePredicate 单页匹配 按裸文本相等 对应原版 PagePredicate
    public sealed record PagePredicate(string Contents) : IValuePredicate<Filterable<string>>
    {
        public static readonly Codec<PagePredicate> Codec = Codecs.String.ComapFlatMap(
            contents => DataResult<PagePredicate>.Success(new PagePredicate(contents)),
            predicate => predicate.Contents);

        public bool Test(Filterable<string> value) => value.Raw == Contents;
    }
}
