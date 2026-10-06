namespace NetCraft.Network.Chat;

//FormattedCharSink rich text character sink, mirrors vanilla net.minecraft.util.FormattedCharSink
//Vanilla is a @FunctionalInterface single-method interface; C# uses a delegate to support lambda arguments
//StringDecomposer callback per codepoint during traversal, returning false stops the traversal
//position is the source string index, style is the current character style, codepoint is the Unicode code point
public delegate bool FormattedCharSink(int position, Style style, int codepoint);

//FormattedCharSequence rich text sequence, mirrors vanilla net.minecraft.util.FormattedCharSequence
//Vanilla is a @FunctionalInterface accept(FormattedCharSink); C# uses a delegate
//Holds an accept delegate invoked during traversal; consuming character by character and returning false stops
//Factory methods live in the FormattedCharSequences static class, mirroring vanilla FormattedCharSequence static methods
public delegate bool FormattedCharSequence(FormattedCharSink output);

//FormattedCharSequences rich text sequence factories, mirroring vanilla FormattedCharSequence static methods
//Codepoint single character; Forward traverses forward, Backward traverses backward, Composite combines multiple segments
//C# delegates cannot have static methods, so the factories go in a separate static class and callers use FormattedCharSequences.Forward(...)
public static class FormattedCharSequences
{
    //Empty empty sequence, accept immediately returns true, mirrors vanilla FormattedCharSequence.EMPTY
    public static readonly FormattedCharSequence Empty = _ => true;

    //Codepoint single-character sequence, mirrors vanilla FormattedCharSequence.codepoint
    public static FormattedCharSequence Codepoint(int codepoint, Style style)
        => output => output(0, style, codepoint);

    //Forward forward traversal of plain text, mirrors vanilla FormattedCharSequence.forward
    //Delegates to StringDecomposer.Iterate for UTF-16 surrogate pair decoding without parsing § color codes
    public static FormattedCharSequence Forward(string text, Style style)
        => string.IsNullOrEmpty(text) ? Empty : output => StringDecomposer.Iterate(text, style, output);

    //Backward backward traversal of plain text, mirrors vanilla FormattedCharSequence.backward
    public static FormattedCharSequence Backward(string text, Style style)
        => string.IsNullOrEmpty(text) ? Empty : output => StringDecomposer.IterateBackwards(text, style, output);

    //Composite combines two segments, mirrors vanilla FormattedCharSequence.fromPair
    //After the first segment completes it continues to the second; either returning false stops
    public static FormattedCharSequence Composite(FormattedCharSequence first, FormattedCharSequence second)
        => output => first(output) && second(output);

    //Composite combines multiple segments, mirrors vanilla FormattedCharSequence.fromList
    public static FormattedCharSequence Composite(IReadOnlyList<FormattedCharSequence> parts)
        => parts.Count switch
        {
            0 => Empty,
            1 => parts[0],
            _ => output =>
            {
                foreach (var part in parts)
                    if (!part(output)) return false;
                return true;
            }
        };

    //Composite variadic combination, mirrors vanilla FormattedCharSequence.composite(parts...)
    public static FormattedCharSequence Composite(params FormattedCharSequence[] parts)
        => Composite((IReadOnlyList<FormattedCharSequence>)parts);
}
