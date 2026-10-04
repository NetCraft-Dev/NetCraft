namespace NetCraft.Network.Chat;

using System.Text;
using NetCraft.Codec;
using NetCraft.Network.Chat.Contents;

//可变组件实现对应原版net.minecraft.network.chat.MutableComponent
//承载组件内容和兄弟列表和样式支持链式修改
//C#接口默认实现不能通过实现类实例直接调用故显式重写转发到Component引用
//但GetString/Visit链有递归风险 GetString直接实现避免转发循环
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

    //从内容创建对应原版MutableComponent.create
    public static MutableComponent Create(ComponentContents contents) => new(contents, new(), Style.Empty);

    public ComponentContents Contents => _contents;
    public IReadOnlyList<Component> Siblings => _siblings;
    public Style Style => _style;

    //GetString直接实现避免转发到接口默认实现引发递归
    //Component.GetString默认实现里Visit调用会虚分派回MutableComponent
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

    //带长度限制的字符串拼接直接实现
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

    //尝试折叠为纯字符串直接实现
    public string? TryCollapseToString()
    {
        if (_contents is not PlainTextContents text) return null;
        if (_siblings.Count > 0 || !_style.IsEmpty) return null;
        return text.Text;
    }

    //展平为组件列表直接实现避免转发递归
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

    //判断是否包含另一组件直接实现
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

    //浅拷贝直接实现返回新MutableComponent
    public MutableComponent PlainCopy() => MutableComponent.Create(_contents);

    //深拷贝直接实现返回新MutableComponent
    public MutableComponent Copy() => new MutableComponent(_contents, new List<Component>(_siblings), _style);

    //设置样式对应原版setStyle
    public MutableComponent SetStyle(Style style)
    {
        _style = style;
        return this;
    }

    //追加文本对应原版append(String)
    public MutableComponent Append(string text)
    {
        if (text.Length == 0) return this;
        return Append(Component.Literal(text));
    }

    //追加组件对应原版append(Component)
    public MutableComponent Append(Component component)
    {
        _siblings.Add(component);
        return this;
    }

    //用updater修改样式对应原版withStyle(UnaryOperator)
    public MutableComponent WithStyle(Func<Style, Style> updater)
    {
        SetStyle(updater(_style));
        return this;
    }

    //合并样式补丁对应原版withStyle(Style)
    public MutableComponent WithStyle(Style patch)
    {
        SetStyle(patch.ApplyTo(_style));
        return this;
    }

    //应用多个ChatFormatting对应原版withStyle(ChatFormatting...)
    public MutableComponent WithStyle(params ChatFormatting[] formats)
    {
        SetStyle(_style.ApplyFormats(formats));
        return this;
    }

    //应用单个ChatFormatting对应原版withStyle(ChatFormatting)
    public MutableComponent WithStyle(ChatFormatting format)
    {
        SetStyle(_style.ApplyFormat(format));
        return this;
    }

    //应用RGB颜色对应原版withColor(int)
    public MutableComponent WithColor(int color)
    {
        SetStyle(_style.WithColor(color));
        return this;
    }

    //应用TextColor对应原版withColor(TextColor)
    public MutableComponent WithColor(TextColor? color)
    {
        SetStyle(_style.WithColor(color));
        return this;
    }

    //应用ChatFormatting颜色对应原版withColor(ChatFormatting)
    public MutableComponent WithColor(ChatFormatting formatting)
    {
        SetStyle(_style.WithColor(formatting));
        return this;
    }

    //应用加粗对应原版withBold
    public MutableComponent WithBold(bool? bold)
    {
        SetStyle(_style.WithBold(bold));
        return this;
    }

    //应用斜体对应原版withItalic
    public MutableComponent WithItalic(bool? italic)
    {
        SetStyle(_style.WithItalic(italic));
        return this;
    }

    //应用下划线对应原版withUnderlined
    public MutableComponent WithUnderlined(bool? underlined)
    {
        SetStyle(_style.WithUnderlined(underlined));
        return this;
    }

    //应用删除线对应原版withStrikethrough
    public MutableComponent WithStrikethrough(bool? strikethrough)
    {
        SetStyle(_style.WithStrikethrough(strikethrough));
        return this;
    }

    //应用混淆对应原版withObfuscated
    public MutableComponent WithObfuscated(bool? obfuscated)
    {
        SetStyle(_style.WithObfuscated(obfuscated));
        return this;
    }

    //应用点击事件对应原版withClickEvent
    public MutableComponent WithClickEvent(ClickEvent? clickEvent)
    {
        SetStyle(_style.WithClickEvent(clickEvent));
        return this;
    }

    //应用悬停事件对应原版withHoverEvent
    public MutableComponent WithHoverEvent(HoverEvent? hoverEvent)
    {
        SetStyle(_style.WithHoverEvent(hoverEvent));
        return this;
    }

    //应用插入文本对应原版withInsertion
    public MutableComponent WithInsertion(string? insertion)
    {
        SetStyle(_style.WithInsertion(insertion));
        return this;
    }

    //应用字体对应原版withFont
    public MutableComponent WithFont(FontDescription? font)
    {
        SetStyle(_style.WithFont(font));
        return this;
    }

    //移除阴影对应原版withoutShadow
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
