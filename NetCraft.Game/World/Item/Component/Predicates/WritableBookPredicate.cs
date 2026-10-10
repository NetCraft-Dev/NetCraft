using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Network;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//WritableBookPredicate writable book predicate, checks whether the page set satisfies the collection predicate
//Maps to vanilla net.minecraft.core.component.predicates.WritableBookPredicate
public sealed record WritableBookPredicate(
    Optional<CollectionPredicate<Filterable<string>, WritableBookPredicate.PagePredicate>> Pages)
    : SingleComponentItemPredicate<WritableBookContent>
{
    //Codec persistence codec, only a pages field, maps to vanilla CODEC
    public static readonly Codec<WritableBookPredicate> Codec = RecordCodecBuilder.Of1(
        CollectionPredicate<Filterable<string>, PagePredicate>.Codec(PagePredicate.Codec)
            .OptionalFieldOf("pages")
            .ForGetter((WritableBookPredicate predicate) => predicate.Pages),
        pages => new WritableBookPredicate(pages));

    public DataComponentType<object> ComponentType => DataComponents.WRITABLE_BOOK_CONTENT;

    public bool MatchesValue(WritableBookContent value) => !Pages.IsPresent || Pages.Get().Test(value.Pages);

    //PagePredicate single page match by raw text equality, maps to vanilla PagePredicate
    public sealed record PagePredicate(string Contents) : IValuePredicate<Filterable<string>>
    {
        public static readonly Codec<PagePredicate> Codec = Codecs.String.ComapFlatMap(
            contents => DataResult<PagePredicate>.Success(new PagePredicate(contents)),
            predicate => predicate.Contents);

        public bool Test(Filterable<string> value) => value.Raw == Contents;
    }
}
