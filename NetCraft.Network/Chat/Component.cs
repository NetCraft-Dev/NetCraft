namespace NetCraft.Network.Chat;

using System.Text;
using NetCraft.Codec;
using NetCraft.Commands;
using NetCraft.DataFixer.Util;
using NetCraft.Network.Chat.Contents;

//Text component interface, maps to vanilla net.minecraft.network.chat.Component
//Inherits IMessage and FormattedText to carry styled multi-segment text content
public interface Component : IMessage, FormattedText
{
    //Style, maps to vanilla getStyle
    Style Style { get; }

    //Contents, maps to vanilla getContents
    ComponentContents Contents { get; }

    //Sibling components at the same level, maps to vanilla getSiblings
    IReadOnlyList<Component> Siblings { get; }

    //Attempts to collapse into a plain string, maps to vanilla tryCollapseToString
    //Returns a string only when the content is plain text with no style and no siblings, otherwise returns null
    string? TryCollapseToString()
    {
        if (Contents is not PlainTextContents text) return null;
        if (Siblings.Count > 0 || !Style.IsEmpty) return null;
        return text.Text;
    }

    //Shallow copy, maps to vanilla plainCopy, copying only the content without style or siblings
    MutableComponent PlainCopy() => MutableComponent.Create(Contents);

    //Deep copy, maps to vanilla copy, copying content, siblings, and style
    MutableComponent Copy() => new MutableComponent(Contents, new List<Component>(Siblings), Style);

    //Styled consumer traversal, maps to vanilla visit(StyledContentConsumer,Style)
    //Aligns with vanilla by visiting its own content first, then recursively visiting siblings
    new Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle)
    {
        var selfStyle = Style.ApplyTo(parentStyle);
        var selfResult = Contents.Visit(output, selfStyle);
        if (selfResult.IsPresent) return selfResult;
        foreach (var sibling in Siblings)
        {
            var result = sibling.Visit(output, selfStyle);
            if (result.IsPresent) return result;
        }
        return Optional<T>.Empty();
    }

    //Unstyled consumer traversal, maps to vanilla visit(ContentConsumer)
    new Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output)
    {
        var selfResult = Contents.Visit(output);
        if (selfResult.IsPresent) return selfResult;
        foreach (var sibling in Siblings)
        {
            var result = sibling.Visit(output);
            if (result.IsPresent) return result;
        }
        return Optional<T>.Empty();
    }

    //IMessage.GetString goes through this unstyled consumer traversal path
    Optional<T> FormattedText.Visit<T>(FormattedText.ContentConsumer<T> output) => Visit(output);

    //FormattedText.Visit styled consumer traversal delegates to Component.Visit
    Optional<T> FormattedText.Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle) => Visit(output, parentStyle);

    //IMessage.GetString is explicitly implemented and delegates to Component.GetString
    string IMessage.GetString() => GetString();

    //Concatenates to a string, maps to vanilla getString
    //Explicitly declared to override both IMessage.GetString and FormattedText.GetString, resolving the multiple-inheritance ambiguity
    new string GetString()
    {
        var builder = new StringBuilder();
        Visit(contents =>
        {
            builder.Append(contents);
            return Optional<object>.Empty();
        });
        return builder.ToString();
    }

    //Length-limited string concatenation, maps to vanilla getString(int)
    string GetString(int limit)
    {
        var builder = new StringBuilder();
        Visit(contents =>
        {
            var remaining = limit - builder.Length;
            if (remaining <= 0) return Optional<object>.Empty();
            builder.Append(contents.Length <= remaining ? contents : contents[..remaining]);
            return Optional<object>.Empty();
        });
        return builder.ToString();
    }

    //Flattens into a component list, maps to vanilla toFlatList(Style)
    List<Component> ToFlatList(Style rootStyle)
    {
        var result = new List<Component>();
        Visit((style, contents) =>
        {
            if (contents.Length > 0)
            {
                result.Add(Literal(contents).WithStyle(style));
            }
            return Optional<object>.Empty();
        }, rootStyle);
        return result;
    }

    List<Component> ToFlatList() => ToFlatList(Style.Empty);

    //Checks whether it contains another component, maps to vanilla contains
    bool Contains(Component other)
    {
        if (Equals(other)) return true;
        var flat = ToFlatList();
        var otherFlat = other.ToFlatList(Style);
        if (otherFlat.Count == 0 || flat.Count < otherFlat.Count) return false;
        for (var i = 0; i <= flat.Count - otherFlat.Count; i++)
        {
            var match = true;
            for (var j = 0; j < otherFlat.Count; j++)
            {
                if (!flat[i + j].Equals(otherFlat[j])) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    //Turns null into an empty component, maps to vanilla nullToEmpty
    public static Component NullToEmpty(string? text) => text is null ? CommonComponents.Empty : Literal(text);

    //Plain text component, maps to vanilla literal
    public static MutableComponent Literal(string text) => MutableComponent.Create(PlainTextContents.Create(text));

    //Translatable component, maps to vanilla translatable
    public static MutableComponent Translatable(string key) => MutableComponent.Create(new TranslatableContents(key, null, TranslatableContents.NoArgs));

    //Translatable component with arguments, maps to vanilla translatable(String,Object[])
    public static MutableComponent Translatable(string key, params object[] args) => MutableComponent.Create(new TranslatableContents(key, null, args));

    //Translatable component with fallback text, maps to vanilla translatableWithFallback(String,String)
    public static MutableComponent TranslatableWithFallback(string key, string? fallback) => MutableComponent.Create(new TranslatableContents(key, fallback, TranslatableContents.NoArgs));

    //Translatable component with fallback text and arguments, maps to vanilla translatableWithFallback(String,String,Object[])
    public static MutableComponent TranslatableWithFallback(string key, string? fallback, params object[] args) => MutableComponent.Create(new TranslatableContents(key, fallback, args));

    //Empty component, maps to vanilla empty
    public static MutableComponent Empty() => MutableComponent.Create(PlainTextContents.Empty);

    //Keybind component, maps to vanilla keybind
    public static MutableComponent Keybind(string name) => MutableComponent.Create(new KeybindContents(name));
}
