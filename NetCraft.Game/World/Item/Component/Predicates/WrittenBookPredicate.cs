using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Network.Component;
using NetCraft.Registry;
//别名避开与上级命名空间段 Component 撞名 命名空间成员优先于 using 别名所以不能同名
using ChatComponent = NetCraft.Network.Chat.Component;

namespace NetCraft.Game.World.Items.Component.Predicates;

//WrittenBookPredicate 成书谓词 判定页集合与作者与标题与世代与解析标记
//对应原版 net.minecraft.core.component.predicates.WrittenBookPredicate
public sealed record WrittenBookPredicate(
    Optional<CollectionPredicate<Filterable<ChatComponent>, WrittenBookPredicate.PagePredicate>> Pages,
    Optional<string> Author,
    Optional<string> Title,
    MinMaxBounds.Ints Generation,
    Optional<bool> Resolved) : SingleComponentItemPredicate<WrittenBookContent>
{
    //Codec 持久化编解码 五字段 对应原版 CODEC
    public static readonly Codec<WrittenBookPredicate> Codec = RecordCodecBuilder.Of5(
        CollectionPredicate<Filterable<ChatComponent>, PagePredicate>.Codec(PagePredicate.Codec)
            .OptionalFieldOf("pages")
            .ForGetter((WrittenBookPredicate predicate) => predicate.Pages),
        Codecs.String.OptionalFieldOf("author").ForGetter((WrittenBookPredicate predicate) => predicate.Author),
        Codecs.String.OptionalFieldOf("title").ForGetter((WrittenBookPredicate predicate) => predicate.Title),
        MinMaxBounds.Ints.CODEC.OptionalFieldOf("generation", MinMaxBounds.Ints.Any)
            .ForGetter((WrittenBookPredicate predicate) => predicate.Generation),
        Codecs.Bool.OptionalFieldOf("resolved").ForGetter((WrittenBookPredicate predicate) => predicate.Resolved),
        (pages, author, title, generation, resolved)
            => new WrittenBookPredicate(pages, author, title, generation, resolved));

    public DataComponentType<object> ComponentType => DataComponents.WRITTEN_BOOK_CONTENT;

    //MatchesValue 标题比的是裸值 其余字段缺省即不约束
    public bool MatchesValue(WrittenBookContent value)
    {
        if (Author.IsPresent && Author.Get() != value.Author) return false;
        if (Title.IsPresent && Title.Get() != value.Title.Raw) return false;
        if (!Generation.Matches(value.Generation)) return false;
        if (Resolved.IsPresent && Resolved.Get() != value.Resolved) return false;
        return !Pages.IsPresent || Pages.Get().Test(value.Pages);
    }

    //PagePredicate 单页匹配 按裸组件相等 对应原版 PagePredicate
    public sealed record PagePredicate(ChatComponent Contents) : IValuePredicate<Filterable<ChatComponent>>
    {
        public static readonly Codec<PagePredicate> Codec = ComponentSerialization.Codec.ComapFlatMap(
            component => DataResult<PagePredicate>.Success(new PagePredicate(component)),
            predicate => predicate.Contents);

        public bool Test(Filterable<ChatComponent> value) => Equals(value.Raw, Contents);
    }
}
