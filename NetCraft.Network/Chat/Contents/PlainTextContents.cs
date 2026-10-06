using NetCraft.Codec;
using NetCraft.DataFixer.Util;

namespace NetCraft.Network.Chat.Contents;

//Plain text contents, maps to vanilla net.minecraft.network.chat.contents.PlainTextContents
//Only carries a string text field; LiteralContents is the implementation and Empty is the empty instance
public interface PlainTextContents : ComponentContents
{
    string Text { get; }

    //Empty empty text instance, maps to vanilla EMPTY
    public static readonly PlainTextContents Empty = new LiteralContents(string.Empty);

    //Create returns Empty for an empty string, otherwise LiteralContents, maps to vanilla create
    public static PlainTextContents Create(string text)
        => text.Length == 0 ? Empty : new LiteralContents(text);

    //Codec returns a MapCodec placeholder, to be completed by the Codec subsystem
    MapCodec<ComponentContents> Codec() => throw new NotImplementedException();
}

//LiteralContents plain text literal implementation, maps to vanilla PlainTextContents.LiteralContents
//Overrides Visit to feed text to the consumer, aligning with vanilla visit behavior
public sealed record LiteralContents(string Text) : PlainTextContents
{
    //Codec is explicitly implemented, to be completed by the Codec subsystem
    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    //Visit unstyled consumer directly invokes the delegate with text
    public Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output) => output(Text);

    //Visit styled consumer invokes the delegate with (style, text)
    public Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style currentStyle)
        => output(currentStyle, Text);

    public override string ToString() => $"literal{{{Text}}}";
}
