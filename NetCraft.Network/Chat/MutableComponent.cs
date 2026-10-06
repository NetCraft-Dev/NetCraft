namespace NetCraft.Network.Chat;

using System.Text;
using NetCraft.Codec;
using NetCraft.Network.Chat.Contents;

//Mutable component implementation, maps to vanilla net.minecraft.network.chat.MutableComponent
//Carries the component content, sibling list, and style, supporting chained modification
//C# interface default implementations cannot be called directly on an implementing instance, so they are explicitly overridden and forwarded to the Component reference
//But the GetString/Visit chain risks recursion, so GetString is implemented directly to avoid a forwarding loop
public sealed class MutableComponent : Component
{
    private readonly ComponentContents _contents;
    private readonly List<Component> _siblings;
    private Style _style;

    public MutableComponent(ComponentContents contents, List<Component> siblings, Style style)
    {
        _contents = contents;
        _siblings = siblings;
        _style = style;
    }

    //Creates from content, maps to vanilla MutableComponent.create
    public static MutableComponent Create(ComponentContents contents) => new(contents, new(), Style.Empty);

    public ComponentContents Contents => _contents;
    public IReadOnlyList<Component> Siblings => _siblings;
    public Style Style => _style;

    //GetString is implemented directly to avoid forwarding to the interface default implementation and causing recursion
    //In the Component.GetString default implementation the Visit call virtually dispatches back to MutableComponent
    public string GetString()
    {
        var builder = new StringBuilder();
        ((Component)this).Visit(contents =>
        {
            builder.Append(contents);
            return Optional<object>.Empty();
        });
        return builder.ToString();
    }

    //Length-limited string concatenation is implemented directly
    public string GetString(int limit)
    {
        var builder = new StringBuilder();
        ((Component)this).Visit(contents =>
        {
            var remaining = limit - builder.Length;
            if (remaining <= 0) return Optional<object>.Empty();
            builder.Append(contents.Length <= remaining ? contents : contents[..remaining]);
            return Optional<object>.Empty();
        });
        return builder.ToString();
    }

    //Attempts to collapse to a plain string are implemented directly
    public string? TryCollapseToString()
    {
        if (_contents is not PlainTextContents text) return null;
        if (_siblings.Count > 0 || !_style.IsEmpty) return null;
        return text.Text;
    }

    //Flattening into a component list is implemented directly to avoid forwarding recursion
    public List<Component> ToFlatList() => ToFlatList(Style.Empty);

    public List<Component> ToFlatList(Style rootStyle)
    {
        var result = new List<Component>();
        ((Component)this).Visit((style, contents) =>
        {
            if (contents.Length > 0)
            {
                result.Add(Component.Literal(contents).WithStyle(style));
            }
            return Optional<object>.Empty();
        }, rootStyle);
        return result;
    }

    //The contains-another-component check is implemented directly
    public bool Contains(Component other)
    {
        if (Equals(other)) return true;
        var flat = ToFlatList();
        var otherFlat = other.ToFlatList(_style);
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

    //The shallow copy is implemented directly, returning a new MutableComponent
    public MutableComponent PlainCopy() => MutableComponent.Create(_contents);

    //The deep copy is implemented directly, returning a new MutableComponent
    public MutableComponent Copy() => new MutableComponent(_contents, new List<Component>(_siblings), _style);

    //Sets the style, maps to vanilla setStyle
    public MutableComponent SetStyle(Style style)
    {
        _style = style;
        return this;
    }

    //Appends text, maps to vanilla append(String)
    public MutableComponent Append(string text)
    {
        if (text.Length == 0) return this;
        return Append(Component.Literal(text));
    }

    //Appends a component, maps to vanilla append(Component)
    public MutableComponent Append(Component component)
    {
        _siblings.Add(component);
        return this;
    }

    //Modifies the style with updater, maps to vanilla withStyle(UnaryOperator)
    public MutableComponent WithStyle(Func<Style, Style> updater)
    {
        SetStyle(updater(_style));
        return this;
    }

    //Merges a style patch, maps to vanilla withStyle(Style)
    public MutableComponent WithStyle(Style patch)
    {
        SetStyle(patch.ApplyTo(_style));
        return this;
    }

    //Applies multiple ChatFormatting, maps to vanilla withStyle(ChatFormatting...)
    public MutableComponent WithStyle(params ChatFormatting[] formats)
    {
        SetStyle(_style.ApplyFormats(formats));
        return this;
    }

    //Applies a single ChatFormatting, maps to vanilla withStyle(ChatFormatting)
    public MutableComponent WithStyle(ChatFormatting format)
    {
        SetStyle(_style.ApplyFormat(format));
        return this;
    }

    //Applies an RGB color, maps to vanilla withColor(int)
    public MutableComponent WithColor(int color)
    {
        SetStyle(_style.WithColor(color));
        return this;
    }

    //Applies a TextColor, maps to vanilla withColor(TextColor)
    public MutableComponent WithColor(TextColor? color)
    {
        SetStyle(_style.WithColor(color));
        return this;
    }

    //Applies a ChatFormatting color, maps to vanilla withColor(ChatFormatting)
    public MutableComponent WithColor(ChatFormatting formatting)
    {
        SetStyle(_style.WithColor(formatting));
        return this;
    }

    //Applies bold, maps to vanilla withBold
    public MutableComponent WithBold(bool? bold)
    {
        SetStyle(_style.WithBold(bold));
        return this;
    }

    //Applies italic, maps to vanilla withItalic
    public MutableComponent WithItalic(bool? italic)
    {
        SetStyle(_style.WithItalic(italic));
        return this;
    }

    //Applies underline, maps to vanilla withUnderlined
    public MutableComponent WithUnderlined(bool? underlined)
    {
        SetStyle(_style.WithUnderlined(underlined));
        return this;
    }

    //Applies strikethrough, maps to vanilla withStrikethrough
    public MutableComponent WithStrikethrough(bool? strikethrough)
    {
        SetStyle(_style.WithStrikethrough(strikethrough));
        return this;
    }

    //Applies obfuscated, maps to vanilla withObfuscated
    public MutableComponent WithObfuscated(bool? obfuscated)
    {
        SetStyle(_style.WithObfuscated(obfuscated));
        return this;
    }

    //Applies a click event, maps to vanilla withClickEvent
    public MutableComponent WithClickEvent(ClickEvent? clickEvent)
    {
        SetStyle(_style.WithClickEvent(clickEvent));
        return this;
    }

    //Applies a hover event, maps to vanilla withHoverEvent
    public MutableComponent WithHoverEvent(HoverEvent? hoverEvent)
    {
        SetStyle(_style.WithHoverEvent(hoverEvent));
        return this;
    }

    //Applies insertion text, maps to vanilla withInsertion
    public MutableComponent WithInsertion(string? insertion)
    {
        SetStyle(_style.WithInsertion(insertion));
        return this;
    }

    //Applies a font, maps to vanilla withFont
    public MutableComponent WithFont(FontDescription? font)
    {
        SetStyle(_style.WithFont(font));
        return this;
    }

    //Removes the shadow, maps to vanilla withoutShadow
    public MutableComponent WithoutShadow()
    {
        SetStyle(_style.WithoutShadow());
        return this;
    }

    public override bool Equals(object? obj)
    {
        if (this == obj) return true;
        if (obj is not MutableComponent that) return false;
        return _contents.Equals(that._contents) && _style.Equals(that._style) && _siblings.SequenceEqual(that._siblings);
    }

    public override int GetHashCode()
    {
        var result = 31 + _contents.GetHashCode();
        result = (31 * result) + _style.GetHashCode();
        foreach (var sibling in _siblings)
        {
            result = (31 * result) + sibling.GetHashCode();
        }
        return result;
    }

    public override string ToString()
    {
        var result = new StringBuilder(_contents.ToString() ?? string.Empty);
        var hasStyle = !_style.IsEmpty;
        var hasSiblings = _siblings.Count > 0;
        if (hasStyle || hasSiblings)
        {
            result.Append('[');
            if (hasStyle)
            {
                result.Append("style=").Append(_style);
            }
            if (hasStyle && hasSiblings)
            {
                result.Append(", ");
            }
            if (hasSiblings)
            {
                result.Append("siblings=").Append('[');
                for (var i = 0; i < _siblings.Count; i++)
                {
                    if (i > 0) result.Append(", ");
                    result.Append(_siblings[i]);
                }
                result.Append(']');
            }
            result.Append(']');
        }
        return result.ToString();
    }
}
