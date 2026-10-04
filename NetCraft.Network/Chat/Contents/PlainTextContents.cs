using NetCraft.Codec;
using NetCraft.DataFixer.Util;

namespace NetCraft.Network.Chat.Contents;

//纯文本内容对应原版net.minecraft.network.chat.contents.PlainTextContents
//仅承载字符串text字段LiteralContents为实现Empty为空实例
public interface PlainTextContents : ComponentContents
{
    string Text { get; }

    //Empty 空文本实例对应原版 EMPTY
    public static readonly PlainTextContents Empty = new LiteralContents(string.Empty);

    //Create 空字符串返回 Empty 否则返回 LiteralContents 对应原版 create
    public static PlainTextContents Create(string text)
        => text.Length == 0 ? Empty : new LiteralContents(text);

    //Codec 返回 MapCodec 占位待 Codec 子系统补全
    MapCodec<ComponentContents> Codec() => throw new NotImplementedException();
}

//LiteralContents 纯文本字面量实现对应原版 PlainTextContents.LiteralContents
//重写 Visit 把 text 喂给消费者对齐原版 visit 行为
public sealed record LiteralContents(string Text) : PlainTextContents
{
    //Codec 显式实现待 Codec 子系统补全
    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    //Visit 无样式消费者直接调用委托传 text
    public Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output) => output(Text);

    //Visit 带样式消费者调用委托传(style, text)
    public Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style currentStyle)
        => output(currentStyle, Text);

    public override string ToString() => $"literal{{{Text}}}";
}
