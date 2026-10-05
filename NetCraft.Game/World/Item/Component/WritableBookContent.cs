using NetCraft.Codec;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//WritableBookContent 书与笔内容 一页一条可过筛文本 上限 100 页
//对应原版 net.minecraft.world.item.component.WritableBookContent
public sealed class WritableBookContent : IEquatable<WritableBookContent>
{
    //PageEditLength 单页编辑长度上限 对应原版 PAGE_EDIT_LENGTH
    public const int PageEditLength = 1024;

    //MaxPages 页数上限 对应原版 MAX_PAGES
    public const int MaxPages = 100;

    //Empty 空书 对应原版 EMPTY
    public static readonly WritableBookContent Empty = new(Array.Empty<Filterable<string>>());

    //PageCodec 单页编解码 对应原版 PAGE_CODEC
    private static readonly Codec<Filterable<string>> PageCodec = Filterable<string>.CodecOf(Codecs.String);

    //Codec 持久化编解码 只有 pages 一个字段 对应原版 CODEC
    public static readonly Codec<WritableBookContent> Codec = RecordCodecBuilder.Of1(
        PageCodec.ListOf().OptionalFieldOf("pages", Array.Empty<Filterable<string>>())
            .ForGetter((WritableBookContent content) => content.Pages),
        pages => new WritableBookContent(pages));

    //StreamCodec 网络编解码 页列表进出 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, WritableBookContent> StreamCodec =
        new WritableBookContentStreamCodec();

    public WritableBookContent(IReadOnlyList<Filterable<string>> pages)
    {
        if (pages.Count > MaxPages) throw new ArgumentException($"页数 {pages.Count} 超过上限 {MaxPages}");
        Pages = pages;
    }

    public IReadOnlyList<Filterable<string>> Pages { get; }

    //GetPages 取每页文本 过滤开关决定看哪一份
    public IEnumerable<string> GetPages(bool filterEnabled) => Pages.Select(page => page.Get(filterEnabled));

    public bool Equals(WritableBookContent? other) => other is not null && Pages.SequenceEqual(other.Pages);

    public override bool Equals(object? obj) => Equals(obj as WritableBookContent);

    public override int GetHashCode() => Pages.Count;

    public override string ToString() => $"WritableBookContent[{Pages.Count} pages]";
}

//WritableBookContentStreamCodec 页列表进出 对应原版 STREAM_CODEC
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
