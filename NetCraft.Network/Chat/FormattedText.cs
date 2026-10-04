namespace NetCraft.Network.Chat;

using System.Text;
using NetCraft.Codec;
using NetCraft.DataFixer.Util;

//格式化文本接口对应原版net.minecraft.network.chat.FormattedText
//提供visit遍历文本的能力供字符串提取与渲染消费
public interface FormattedText
{
    //停止迭代标记对应原版STOP_ITERATION
    public static readonly Optional<Unit> STOP_ITERATION = Optional<Unit>.Of(Unit.Instance);

    //空格式化文本单例
    public static readonly FormattedText EMPTY = new EmptyFormattedText();

    //无样式消费者遍历对应原版visit(ContentConsumer)
    Optional<T> Visit<T>(ContentConsumer<T> output);

    //带样式消费者遍历对应原版visit(StyledContentConsumer,Style)
    Optional<T> Visit<T>(StyledContentConsumer<T> output, Style parentStyle);

    //默认字符串拼接实现对应原版getString
    string GetString()
    {
        var builder = new StringBuilder();
        Visit(contents =>
        {
            builder.Append(contents);
            return Optional<object>.Empty();
        });
        return builder.ToString();
    }

    //从纯文本构造FormattedText对应原版of(String)
    public static FormattedText Of(string text) => new TextFormattedText(text);

    //从带样式文本构造FormattedText对应原版of(String,Style)
    public static FormattedText Of(string text, Style style) => new StyledTextFormattedText(text, style);

    //组合多个FormattedText对应原版composite
    public static FormattedText Composite(params FormattedText[] parts) => new CompositeFormattedText(parts);

    //组合列表版对应原版composite(List)
    public static FormattedText Composite(IReadOnlyList<FormattedText> parts) => new CompositeFormattedText(parts);

    //无样式内容消费者委托
    public delegate Optional<T> ContentConsumer<T>(string contents);

    //带样式内容消费者委托
    public delegate Optional<T> StyledContentConsumer<T>(Style style, string contents);
}

//空实现单例
internal sealed class EmptyFormattedText : FormattedText
{
    public Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output) => Optional<T>.Empty();
    public Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle) => Optional<T>.Empty();
}

//纯文本实现
internal sealed class TextFormattedText(string text) : FormattedText
{
    public Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output) => output(text);
    public Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle) => output(parentStyle, text);
}

//带样式文本实现
internal sealed class StyledTextFormattedText(string text, Style style) : FormattedText
{
    public Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output) => output(text);
    public Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle) => output(style.ApplyTo(parentStyle), text);
}

//组合实现遍历多个部分
internal sealed class CompositeFormattedText(IReadOnlyList<FormattedText> parts) : FormattedText
{
    public Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output)
    {
        foreach (var part in parts)
        {
            var result = part.Visit(output);
            if (result.IsPresent) return result;
        }
        return Optional<T>.Empty();
    }

    public Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle)
    {
        foreach (var part in parts)
        {
            var result = part.Visit(output, parentStyle);
            if (result.IsPresent) return result;
        }
        return Optional<T>.Empty();
    }
}
