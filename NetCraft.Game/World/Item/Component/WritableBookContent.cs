using NetCraft.Codec;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//WritableBookContent book and quill contents, one filterable text per page, limit 100 pages
//Maps to vanilla net.minecraft.world.item.component.WritableBookContent
public sealed class WritableBookContent : IEquatable<WritableBookContent>
{
    //PageEditLength per-page edit length limit, maps to vanilla PAGE_EDIT_LENGTH
    public const int PageEditLength = 1024;

    //MaxPages page limit, maps to vanilla MAX_PAGES
    public const int MaxPages = 100;

    //Empty empty book, maps to vanilla EMPTY
    public static readonly WritableBookContent Empty = new(Array.Empty<Filterable<string>>());

    //PageCodec single page codec, maps to vanilla PAGE_CODEC
    private static readonly Codec<Filterable<string>> PageCodec = Filterable<string>.CodecOf(Codecs.String);

    //Codec persistence codec, only a pages field, maps to vanilla CODEC
    public static readonly Codec<WritableBookContent> Codec = RecordCodecBuilder.Of1(
        PageCodec.ListOf().OptionalFieldOf("pages", Array.Empty<Filterable<string>>())
            .ForGetter((WritableBookContent content) => content.Pages),
        pages => new WritableBookContent(pages));

    //StreamCodec network codec, the page list goes in and out, maps to vanilla STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, WritableBookContent> StreamCodec =
        new WritableBookContentStreamCodec();

    public WritableBookContent(IReadOnlyList<Filterable<string>> pages)
    {
        if (pages.Count > MaxPages) throw new ArgumentException($"page count {pages.Count} exceeds the limit {MaxPages}");
        Pages = pages;
    }

    public IReadOnlyList<Filterable<string>> Pages { get; }

    //GetPages returns the text of each page, the filter flag selects which copy is read
    public IEnumerable<string> GetPages(bool filterEnabled) => Pages.Select(page => page.Get(filterEnabled));

    public bool Equals(WritableBookContent? other) => other is not null && Pages.SequenceEqual(other.Pages);

    public override bool Equals(object? obj) => Equals(obj as WritableBookContent);

    public override int GetHashCode() => Pages.Count;

    public override string ToString() => $"WritableBookContent[{Pages.Count} pages]";
}

//WritableBookContentStreamCodec the page list goes in and out, maps to vanilla STREAM_CODEC
internal sealed class WritableBookContentStreamCodec : StreamCodec<RegistryFriendlyByteBuf, WritableBookContent>
{
    private static readonly StreamCodec<RegistryFriendlyByteBuf, Filterable<string>> PageCodec =
        Filterable<string>.StreamCodecOf(ByteBufCodecs.StringUtf8(WritableBookContent.PageEditLength));

    public WritableBookContent Decode(RegistryFriendlyByteBuf buf)
    {
        var size = buf.ReadVarInt();
        var pages = new List<Filterable<string>>(Math.Min(size, ByteBufCodecs.MaxInitialCollectionSize));
        for (var i = 0; i < size; i++) pages.Add(PageCodec.Decode(buf));
        return new WritableBookContent(pages);
    }

    public void Encode(RegistryFriendlyByteBuf buf, WritableBookContent value)
    {
        buf.WriteVarInt(value.Pages.Count);
        foreach (var page in value.Pages) PageCodec.Encode(buf, page);
    }
}
