namespace NetCraft.Network.Chat;

using System.Text;

//文本样式对应原版net.minecraft.network.chat.Style
//承载颜色/加粗/斜体/下划线/删除线/混淆/点击事件/悬停事件/插入文本/字体描述
public sealed class Style
{
    public static readonly Style Empty = new(null, null, null, null, null, null, null, null, null, null, null);

    private readonly TextColor? _color;
    private readonly int? _shadowColor;
    private readonly bool? _bold;
    private readonly bool? _italic;
    private readonly bool? _underlined;
    private readonly bool? _strikethrough;
    private readonly bool? _obfuscated;
    private readonly ClickEvent? _clickEvent;
    private readonly HoverEvent? _hoverEvent;
    private readonly string? _insertion;
    private readonly FontDescription? _font;

    private Style(
        TextColor? color,
        int? shadowColor,
        bool? bold,
        bool? italic,
        bool? underlined,
        bool? strikethrough,
        bool? obfuscated,
        ClickEvent? clickEvent,
        HoverEvent? hoverEvent,
        string? insertion,
        FontDescription? font)
    {
        _color = color;
        _shadowColor = shadowColor;
        _bold = bold;
        _italic = italic;
        _underlined = underlined;
        _strikethrough = strikethrough;
        _obfuscated = obfuscated;
        _clickEvent = clickEvent;
        _hoverEvent = hoverEvent;
        _insertion = insertion;
        _font = font;
    }

    public TextColor? Color => _color;
    public int? ShadowColor => _shadowColor;
    public bool IsBold => _bold == true;
    public bool IsItalic => _italic == true;
    public bool IsStrikethrough => _strikethrough == true;
    public bool IsUnderlined => _underlined == true;
    public bool IsObfuscated => _obfuscated == true;
    public bool IsEmpty => this == Empty;
    public ClickEvent? ClickEvent => _clickEvent;
    public HoverEvent? HoverEvent => _hoverEvent;
    public string? Insertion => _insertion;
    public FontDescription Font => _font ?? FontDescription.Default;

    //应用颜色返回新样式对应原版withColor(TextColor)
    public Style WithColor(TextColor? color)
    {
        if (_color == color) return this;
        return CheckEmptyAfterChange(new(color, _shadowColor, _bold, _italic, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font), _color, color);
    }

    //应用ChatFormatting颜色对应原版withColor(ChatFormatting)
    public Style WithColor(ChatFormatting? color)
        => WithColor(color is null ? null : TextColor.FromLegacyFormat(color));

    //应用RGB颜色对应原版withColor(int)
    public Style WithColor(int color)
        => WithColor(TextColor.FromRgb(color));

    public Style WithShadowColor(int shadowColor)
    {
        if (_shadowColor == shadowColor) return this;
        return CheckEmptyAfterChange(new(_color, shadowColor, _bold, _italic, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font), _shadowColor, shadowColor);
    }

    public Style WithoutShadow() => WithShadowColor(0);

    public Style WithBold(bool? bold)
    {
        if (_bold == bold) return this;
        return CheckEmptyAfterChange(new(_color, _shadowColor, bold, _italic, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font), _bold, bold);
    }

    public Style WithItalic(bool? italic)
    {
        if (_italic == italic) return this;
        return CheckEmptyAfterChange(new(_color, _shadowColor, _bold, italic, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font), _italic, italic);
    }

    public Style WithUnderlined(bool? underlined)
    {
        if (_underlined == underlined) return this;
        return CheckEmptyAfterChange(new(_color, _shadowColor, _bold, _italic, underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font), _underlined, underlined);
    }

    public Style WithStrikethrough(bool? strikethrough)
    {
        if (_strikethrough == strikethrough) return this;
        return CheckEmptyAfterChange(new(_color, _shadowColor, _bold, _italic, _underlined, strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font), _strikethrough, strikethrough);
    }

    public Style WithObfuscated(bool? obfuscated)
    {
        if (_obfuscated == obfuscated) return this;
        return CheckEmptyAfterChange(new(_color, _shadowColor, _bold, _italic, _underlined, _strikethrough, obfuscated, _clickEvent, _hoverEvent, _insertion, _font), _obfuscated, obfuscated);
    }

    public Style WithClickEvent(ClickEvent? clickEvent)
    {
        if (_clickEvent == clickEvent) return this;
        return CheckEmptyAfterChange(new(_color, _shadowColor, _bold, _italic, _underlined, _strikethrough, _obfuscated, clickEvent, _hoverEvent, _insertion, _font), _clickEvent, clickEvent);
    }

    public Style WithHoverEvent(HoverEvent? hoverEvent)
    {
        if (_hoverEvent == hoverEvent) return this;
        return CheckEmptyAfterChange(new(_color, _shadowColor, _bold, _italic, _underlined, _strikethrough, _obfuscated, _clickEvent, hoverEvent, _insertion, _font), _hoverEvent, hoverEvent);
    }

    public Style WithInsertion(string? insertion)
    {
        if (_insertion == insertion) return this;
        return CheckEmptyAfterChange(new(_color, _shadowColor, _bold, _italic, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, insertion, _font), _insertion, insertion);
    }

    public Style WithFont(FontDescription? font)
    {
        if (_font == font) return this;
        return CheckEmptyAfterChange(new(_color, _shadowColor, _bold, _italic, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, font), _font, font);
    }

    //应用单个ChatFormatting对应原版applyFormat
    public Style ApplyFormat(ChatFormatting format)
    {
        if (format == ChatFormatting.Reset) return Empty;
        if (format == ChatFormatting.Obfuscated) return new(_color, _shadowColor, _bold, _italic, _underlined, _strikethrough, true, _clickEvent, _hoverEvent, _insertion, _font);
        if (format == ChatFormatting.Bold) return new(_color, _shadowColor, true, _italic, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font);
        if (format == ChatFormatting.Strikethrough) return new(_color, _shadowColor, _bold, _italic, _underlined, true, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font);
        if (format == ChatFormatting.Underline) return new(_color, _shadowColor, _bold, _italic, true, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font);
        if (format == ChatFormatting.Italic) return new(_color, _shadowColor, _bold, true, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font);
        var color = TextColor.FromLegacyFormat(format);
        return new(color, _shadowColor, _bold, _italic, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font);
    }

    //应用旧式ChatFormatting重置格式标志对应原版applyLegacyFormat
    public Style ApplyLegacyFormat(ChatFormatting format)
    {
        if (format == ChatFormatting.Reset) return Empty;
        if (format == ChatFormatting.Obfuscated) return new(_color, _shadowColor, _bold, _italic, _underlined, _strikethrough, true, _clickEvent, _hoverEvent, _insertion, _font);
        if (format == ChatFormatting.Bold) return new(_color, _shadowColor, true, _italic, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font);
        if (format == ChatFormatting.Strikethrough) return new(_color, _shadowColor, _bold, _italic, _underlined, true, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font);
        if (format == ChatFormatting.Underline) return new(_color, _shadowColor, _bold, _italic, true, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font);
        if (format == ChatFormatting.Italic) return new(_color, _shadowColor, _bold, true, _underlined, _strikethrough, _obfuscated, _clickEvent, _hoverEvent, _insertion, _font);
        var color = TextColor.FromLegacyFormat(format);
        return new(color, _shadowColor, false, false, false, false, false, _clickEvent, _hoverEvent, _insertion, _font);
    }

    //批量应用ChatFormatting对应原版applyFormats
    public Style ApplyFormats(params ChatFormatting[] formats)
    {
        var color = _color;
        var bold = _bold;
        var italic = _italic;
        var strikethrough = _strikethrough;
        var underlined = _underlined;
        var obfuscated = _obfuscated;
        foreach (var format in formats)
        {
            if (format == ChatFormatting.Reset) return Empty;
            if (format == ChatFormatting.Obfuscated) obfuscated = true;
            else if (format == ChatFormatting.Bold) bold = true;
            else if (format == ChatFormatting.Strikethrough) strikethrough = true;
            else if (format == ChatFormatting.Underline) underlined = true;
            else if (format == ChatFormatting.Italic) italic = true;
            else color = TextColor.FromLegacyFormat(format);
        }
        return new(color, _shadowColor, bold, italic, underlined, strikethrough, obfuscated, _clickEvent, _hoverEvent, _insertion, _font);
    }

    //合并样式到other对应原版applyTo非空字段覆盖other
    public Style ApplyTo(Style other)
    {
        if (this == Empty) return other;
        if (other == Empty) return this;
        return new(
            _color ?? other._color,
            _shadowColor ?? other._shadowColor,
            _bold ?? other._bold,
            _italic ?? other._italic,
            _underlined ?? other._underlined,
            _strikethrough ?? other._strikethrough,
            _obfuscated ?? other._obfuscated,
            _clickEvent ?? other._clickEvent,
            _hoverEvent ?? other._hoverEvent,
            _insertion ?? other._insertion,
            _font ?? other._font);
    }

    public override string ToString()
    {
        var builder = new StringBuilder("{");
        var isFirst = true;
        void PrependSeparator()
        {
            if (!isFirst) builder.Append(',');
            isFirst = false;
        }
        void AddFlagString(string name, bool? value)
        {
            if (value is null) return;
            PrependSeparator();
            if (!value.Value) builder.Append('!');
            builder.Append(name);
        }
        void AddValueString(string name, object? value)
        {
            if (value is null) return;
            PrependSeparator();
            builder.Append(name).Append('=').Append(value);
        }
        AddValueString("color", _color);
        AddValueString("shadowColor", _shadowColor);
        AddFlagString("bold", _bold);
        AddFlagString("italic", _italic);
        AddFlagString("underlined", _underlined);
        AddFlagString("strikethrough", _strikethrough);
        AddFlagString("obfuscated", _obfuscated);
        AddValueString("clickEvent", _clickEvent);
        AddValueString("hoverEvent", _hoverEvent);
        AddValueString("insertion", _insertion);
        AddValueString("font", _font);
        builder.Append('}');
        return builder.ToString();
    }

    public override bool Equals(object? obj)
    {
        if (this == obj) return true;
        if (obj is not Style style) return false;
        return _bold == style._bold
            && Equals(_color, style._color)
            && _shadowColor == style._shadowColor
            && _italic == style._italic
            && _obfuscated == style._obfuscated
            && _strikethrough == style._strikethrough
            && _underlined == style._underlined
            && Equals(_clickEvent, style._clickEvent)
            && Equals(_hoverEvent, style._hoverEvent)
            && _insertion == style._insertion
            && Equals(_font, style._font);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_color, _shadowColor, _bold, _italic, _underlined, _strikethrough, _obfuscated, _clickEvent);
    }

    private static Style CheckEmptyAfterChange<T>(Style newStyle, T? previous, T? next)
    {
        if (previous is not null && next is null && newStyle.Equals(Empty)) return Empty;
        return newStyle;
    }
}
