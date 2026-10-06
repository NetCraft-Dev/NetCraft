namespace NetCraft.Network.Chat;

using System.Text;
using NetCraft.Codec;
using NetCraft.DataFixer.Util;

//Formatted text interface, maps to vanilla net.minecraft.network.chat.FormattedText
//Provides the visit capability to traverse text, used for string extraction and rendering consumption
public interface FormattedText
{
    //Stop-iteration marker, maps to vanilla STOP_ITERATION
    public static readonly Optional<Unit> STOP_ITERATION = Optional<Unit>.Of(Unit.Instance);

    //Empty formatted text singleton
    public static readonly FormattedText EMPTY = new EmptyFormattedText();

    //Unstyled consumer traversal, maps to vanilla visit(ContentConsumer)
    Optional<T> Visit<T>(ContentConsumer<T> output);

    //Styled consumer traversal, maps to vanilla visit(StyledContentConsumer,Style)
    Optional<T> Visit<T>(StyledContentConsumer<T> output, Style parentStyle);

    //Default string concatenation implementation, maps to vanilla getString
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

    //Builds FormattedText from plain text, maps to vanilla of(String)
    public static FormattedText Of(string text) => new TextFormattedText(text);

    //Builds FormattedText from styled text, maps to vanilla of(String,Style)
    public static FormattedText Of(string text, Style style) => new StyledTextFormattedText(text, style);

    //Combines multiple FormattedText, maps to vanilla composite
    public static FormattedText Composite(params FormattedText[] parts) => new CompositeFormattedText(parts);

    //List version of the combination, maps to vanilla composite(List)
    public static FormattedText Composite(IReadOnlyList<FormattedText> parts) => new CompositeFormattedText(parts);

    //Unstyled content consumer delegate
    public delegate Optional<T> ContentConsumer<T>(string contents);

    //Styled content consumer delegate
    public delegate Optional<T> StyledContentConsumer<T>(Style style, string contents);
}

//Empty implementation singleton
internal sealed class EmptyFormattedText : FormattedText
{
    public Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output) => Optional<T>.Empty();
    public Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle) => Optional<T>.Empty();
}

//Plain text implementation
internal sealed class TextFormattedText(string text) : FormattedText
{
    public Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output) => output(text);
    public Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle) => output(parentStyle, text);
}

//Styled text implementation
internal sealed class StyledTextFormattedText(string text, Style style) : FormattedText
{
    public Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output) => output(text);
    public Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle) => output(style.ApplyTo(parentStyle), text);
}

//Composite implementation traversing multiple parts
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
