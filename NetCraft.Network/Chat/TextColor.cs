namespace NetCraft.Network.Chat;

using System.Globalization;

//文本颜色对应原版net.minecraft.network.chat.TextColor
//支持命名颜色和RGB自定义颜色
public sealed class TextColor
{
    private const string CustomColorPrefix = "#";

    private static readonly Dictionary<string, TextColor> NamedColors = new();

    public static readonly TextColor Black = Named("black", 0);
    public static readonly TextColor DarkBlue = Named("dark_blue", 170);
    public static readonly TextColor DarkGreen = Named("dark_green", 43520);
    public static readonly TextColor DarkAqua = Named("dark_aqua", 43690);
    public static readonly TextColor DarkRed = Named("dark_red", 11141120);
    public static readonly TextColor DarkPurple = Named("dark_purple", 11141290);
    public static readonly TextColor Gold = Named("gold", 16755200);
    public static readonly TextColor Gray = Named("gray", 11184810);
    public static readonly TextColor DarkGray = Named("dark_gray", 5592405);
    public static readonly TextColor Blue = Named("blue", 5592575);
    public static readonly TextColor Green = Named("green", 5635925);
    public static readonly TextColor Aqua = Named("aqua", 5636095);
    public static readonly TextColor Red = Named("red", 16733525);
    public static readonly TextColor LightPurple = Named("light_purple", 16733695);
    public static readonly TextColor Yellow = Named("yellow", 16777045);
    public static readonly TextColor White = Named("white", 16777215);

    private readonly int _value;
    private readonly string? _name;

    private TextColor(int value, string? name)
    {
        _value = value & 0xFFFFFF;
        _name = name;
    }

    public int Value => _value;

    public string Serialize() => _name ?? FormatValue();

    private string FormatValue() => string.Format(CultureInfo.InvariantCulture, "#{0:X6}", _value);

    public static TextColor FromLegacyFormat(ChatFormatting format)
    {
        if (format == ChatFormatting.Black) return Black;
        if (format == ChatFormatting.DarkBlue) return DarkBlue;
        if (format == ChatFormatting.DarkGreen) return DarkGreen;
        if (format == ChatFormatting.DarkAqua) return DarkAqua;
        if (format == ChatFormatting.DarkRed) return DarkRed;
        if (format == ChatFormatting.DarkPurple) return DarkPurple;
        if (format == ChatFormatting.Gold) return Gold;
        if (format == ChatFormatting.Gray) return Gray;
        if (format == ChatFormatting.DarkGray) return DarkGray;
        if (format == ChatFormatting.Blue) return Blue;
        if (format == ChatFormatting.Green) return Green;
        if (format == ChatFormatting.Aqua) return Aqua;
        if (format == ChatFormatting.Red) return Red;
        if (format == ChatFormatting.LightPurple) return LightPurple;
        if (format == ChatFormatting.Yellow) return Yellow;
        if (format == ChatFormatting.White) return White;
        return null!;
    }

    public static TextColor FromRgb(int rgb) => new(rgb, null);

    public static TextColor? ParseColor(string color)
    {
        if (color.StartsWith(CustomColorPrefix))
        {
            try
            {
                var value = int.Parse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (value < 0 || value > 0xFFFFFF) return null;
                return FromRgb(value);
            }
            catch (FormatException)
            {
                return null;
            }
        }
        return NamedColors.TryGetValue(color, out var predefined) ? predefined : null;
    }

    private static TextColor Named(string name, int rgb)
    {
        var result = new TextColor(rgb, name);
        NamedColors[name] = result;
        return result;
    }

    public override bool Equals(object? obj)
    {
        if (this == obj) return true;
        if (obj is not TextColor other) return false;
        return _value == other._value;
    }

    public override int GetHashCode() => HashCode.Combine(_value, _name);

    //==运算符按值比较对齐Java record equals语义
    public static bool operator ==(TextColor? left, TextColor? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(TextColor? left, TextColor? right) => !(left == right);

    public override string ToString() => Serialize();
}
