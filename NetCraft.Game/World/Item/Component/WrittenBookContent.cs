using NetCraft.Codec;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Util;
//Alias avoids clashing with Component, the last segment of the enclosing namespace; namespace members take precedence over using aliases so the names cannot match
using ChatComponent = NetCraft.Network.Chat.Component;

namespace NetCraft.Game.World.Items.Component;

//WrittenBookContent written book contents: title plus author plus generation plus page list plus resolved flag
//Maps to vanilla net.minecraft.world.item.component.WrittenBookContent
public sealed class WrittenBookContent : IEquatable<WrittenBookContent>
{
    //TitleMaxLength title length limit, maps to vanilla TITLE_MAX_LENGTH
    public const int TitleMaxLength = 32;

    //MaxGeneration generation limit, maps to vanilla MAX_GENERATION
    public const int MaxGeneration = 3;

    //PageListCodec page list codec, each page is a filterable component
    private static readonly Codec<IReadOnlyList<Filterable<ChatComponent>>> PageListCodec =
        Filterable<ChatComponent>.CodecOf(ComponentSerialization.Codec).ListOf();

    //Codec persistence codec, five fields, maps to vanilla CODEC
    public static readonly Codec<WrittenBookContent> Codec = RecordCodecBuilder.Of5(
        Filterable<string>.CodecOf(Codecs.String).FieldOf("title")
            .ForGetter((WrittenBookContent content) => content.Title),
        Codecs.String.FieldOf("author").ForGetter((WrittenBookContent content) => content.Author),
        ExtraCodecs.IntRange(0, MaxGeneration).OptionalFieldOf("generation", 0)
            .ForGetter((WrittenBookContent content) => content.Generation),
        PageListCodec.OptionalFieldOf("pages", Array.Empty<Filterable<ChatComponent>>())
            .ForGetter((WrittenBookContent content) => content.Pages),
        Codecs.Bool.OptionalFieldOf("resolved", false).ForGetter((WrittenBookContent content) => content.Resolved),
        (title, author, generation, pages, resolved)
            => new WrittenBookContent(title, author, generation, pages, resolved));

    //StreamCodec network codec, maps to vanilla STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, WrittenBookContent> StreamCodec =
        new WrittenBookContentStreamCodec();

    //Empty empty written book, maps to vanilla EMPTY
    public static readonly WrittenBookContent Empty = new(
        Filterable<string>.PassThrough(""), "", 0, Array.Empty<Filterable<ChatComponent>>(), true);

    public WrittenBookContent(
        Filterable<string> title, string author, int generation,
        IReadOnlyList<Filterable<ChatComponent>> pages, bool resolved)
    {
        if (generation < 0 || generation > MaxGeneration)
            throw new ArgumentException($"generation {generation} is outside the range 0 to {MaxGeneration}");
        Title = title;
        Author = author;
        Generation = generation;
        Pages = pages;
        Resolved = resolved;
    }

    public Filterable<string> Title { get; }
    public string Author { get; }
    public int Generation { get; }
    public IReadOnlyList<Filterable<ChatComponent>> Pages { get; }
    public bool Resolved { get; }

    //CraftCopy copies it and increments the generation, maps to vanilla craftCopy
    public WrittenBookContent CraftCopy()
        => new(Title, Author, Generation + 1, Pages, Resolved);

    //MarkResolved marks it as resolved, maps to vanilla markResolved
    public WrittenBookContent MarkResolved() => new(Title, Author, Generation, Pages, true);

    public bool Equals(WrittenBookContent? other)
        => other is not null
           && Author == other.Author
           && Generation == other.Generation
           && Resolved == other.Resolved
           && Title == other.Title
           && Pages.SequenceEqual(other.Pages);

    public override bool Equals(object? obj) => Equals(obj as WrittenBookContent);

    public override int GetHashCode() => HashCode.Combine(Title, Author, Generation, Resolved);

    public override string ToString() => $"WrittenBookContent[{Author}, generation={Generation}, pages={Pages.Count}]";
}

//WrittenBookContentStreamCodec title plus author plus generation plus page list plus resolved flag, maps to vanilla STREAM_CODEC
internal sealed class WrittenBookContentStreamCodec : StreamCodec<RegistryFriendlyByteBuf, WrittenBookContent>
{
    private static readonly StreamCodec<RegistryFriendlyByteBuf, Filterable<string>> TitleCodec =
        Filterable<string>.StreamCodecOf(ByteBufCodecs.StringUtf8(WrittenBookContent.TitleMaxLength));

    private static readonly StreamCodec<RegistryFriendlyByteBuf, Filterable<ChatComponent>> PageCodec =
        Filterable<ChatComponent>.StreamCodecOf(ComponentSerialization.StreamCodec);

    public WrittenBookContent Decode(RegistryFriendlyByteBuf buf)
    {
        var title = TitleCodec.Decode(buf);
        var author = buf.ReadString();
        var generation = buf.ReadVarInt();
        var size = buf.ReadVarInt();
        var pages = new List<Filterable<ChatComponent>>(Math.Min(size, ByteBufCodecs.MaxInitialCollectionSize));
        for (var i = 0; i < size; i++) pages.Add(PageCodec.Decode(buf));
        var resolved = buf.ReadBoolean();
        return new WrittenBookContent(title, author, generation, pages, resolved);
    }

    public void Encode(RegistryFriendlyByteBuf buf, WrittenBookContent value)
    {
        TitleCodec.Encode(buf, value.Title);
        buf.WriteString(value.Author);
        buf.WriteVarInt(value.Generation);
        buf.WriteVarInt(value.Pages.Count);
        foreach (var page in value.Pages) PageCodec.Encode(buf, page);
        buf.WriteBoolean(value.Resolved);
    }
}
