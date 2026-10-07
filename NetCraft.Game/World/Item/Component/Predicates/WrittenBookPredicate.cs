using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Network.Component;
using NetCraft.Registry;
//Alias avoids clashing with Component, a namespace segment above; namespace members take precedence over using aliases so the names cannot match
using ChatComponent = NetCraft.Network.Chat.Component;

namespace NetCraft.Game.World.Items.Component.Predicates;

//WrittenBookPredicate written book predicate, checks the page set, author, title, generation and resolved flag
//Maps to vanilla net.minecraft.core.component.predicates.WrittenBookPredicate
public sealed record WrittenBookPredicate(
    Optional<CollectionPredicate<Filterable<ChatComponent>, WrittenBookPredicate.PagePredicate>> Pages,
    Optional<string> Author,
    Optional<string> Title,
    MinMaxBounds.Ints Generation,
    Optional<bool> Resolved) : SingleComponentItemPredicate<WrittenBookContent>
{
    //Codec persistence codec, five fields, maps to vanilla CODEC
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

    //MatchesValue the title is compared as a raw value, the other fields are unconstrained when absent
    public bool MatchesValue(WrittenBookContent value)
    {
        if (Author.IsPresent && Author.Get() != value.Author) return false;
        if (Title.IsPresent && Title.Get() != value.Title.Raw) return false;
        if (!Generation.Matches(value.Generation)) return false;
        if (Resolved.IsPresent && Resolved.Get() != value.Resolved) return false;
        return !Pages.IsPresent || Pages.Get().Test(value.Pages);
    }

    //PagePredicate single page match by raw component equality, maps to vanilla PagePredicate
    public sealed record PagePredicate(ChatComponent Contents) : IValuePredicate<Filterable<ChatComponent>>
    {
        public static readonly Codec<PagePredicate> Codec = ComponentSerialization.Codec.ComapFlatMap(
            component => DataResult<PagePredicate>.Success(new PagePredicate(component)),
            predicate => predicate.Contents);

        public bool Test(Filterable<ChatComponent> value) => Equals(value.Raw, Contents);
    }
}
