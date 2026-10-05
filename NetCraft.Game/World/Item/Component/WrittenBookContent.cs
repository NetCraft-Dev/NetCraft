using NetCraft.Codec;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Util;
//别名避开与所在命名空间末段 Component 撞名 命名空间成员优先于 using 别名所以不能同名
using ChatComponent = NetCraft.Network.Chat.Component;

namespace NetCraft.Game.World.Items.Component;

//WrittenBookContent 成书内容 标题加作者加世代加页列表加是否已解析
//对应原版 net.minecraft.world.item.component.WrittenBookContent
public sealed class WrittenBookContent : IEquatable<WrittenBookContent>
{
    //TitleMaxLength 标题长度上限 对应原版 TITLE_MAX_LENGTH
    public const int TitleMaxLength = 32;

    //MaxGeneration 世代上限 对应原版 MAX_GENERATION
    public const int MaxGeneration = 3;

    //PageListCodec 页列表编解码 每页是可过筛组件
    private static readonly Codec<IReadOnlyList<Filterable<ChatComponent>>> PageListCodec =
        Filterable<ChatComponent>.CodecOf(ComponentSerialization.Codec).ListOf();

    //Codec 持久化编解码 五字段 对应原版 CODEC
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

    //StreamCodec 网络编解码 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, WrittenBookContent> StreamCodec =
        new WrittenBookContentStreamCodec();

    //Empty 空成书 对应原版 EMPTY
    public static readonly WrittenBookContent Empty = new(
        Filterable<string>.PassThrough(""), "", 0, Array.Empty<Filterable<ChatComponent>>(), true);

    public WrittenBookContent(
        Filterable<string> title, string author, int generation,
        IReadOnlyList<Filterable<ChatComponent>> pages, bool resolved)
    {
        if (generation < 0 || generation > MaxGeneration)
            throw new ArgumentException($"世代 {generation} 超出 0 到 {MaxGeneration} 的范围");
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

    //CraftCopy 抄一份并让世代加一 对应原版 craftCopy
    public WrittenBookContent CraftCopy()
        => new(Title, Author, Generation + 1, Pages, Resolved);

    //MarkResolved 标记为已解析 对应原版 markResolved
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

//WrittenBookContentStreamCodec 标题加作者加世代加页列表加解析标记 对应原版 STREAM_CODEC
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
