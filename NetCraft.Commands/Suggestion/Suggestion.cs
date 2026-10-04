using System.Text;

using NetCraft.Commands.Context;

namespace NetCraft.Commands.Suggestion;

//Suggestion 建议项对应原版com.mojang.brigadier.suggestion.Suggestion
//承载range定位的替换文本与可选tooltip供命令补全使用
public class Suggestion : IComparable<Suggestion>, IEquatable<Suggestion>
{
    private readonly StringRange _range;
    private readonly string _text;
    private readonly IMessage? _tooltip;

    public Suggestion(StringRange range, string text) : this(range, text, null)
    {
    }

    public Suggestion(StringRange range, string text, IMessage? tooltip)
    {
        _range = range;
        _text = text;
        _tooltip = tooltip;
    }

    public StringRange Range => _range;
    public string Text => _text;
    public IMessage? Tooltip => _tooltip;

    //Apply 把建议文本应用到原输入对应range区间
    public string Apply(string input)
    {
        if (_range.Start == 0 && _range.End == input.Length)
        {
            return _text;
        }
        var result = new StringBuilder();
        if (_range.Start > 0)
        {
            result.Append(input[.._range.Start]);
        }
        result.Append(_text);
        if (_range.End < input.Length)
        {
            result.Append(input[_range.End..]);
        }
        return result.ToString();
    }

    //Expand 扩展range到指定range并补全两侧原文片段
    public Suggestion Expand(string command, StringRange range)
    {
        if (range == _range)
        {
            return this;
        }
        var result = new StringBuilder();
        if (range.Start < _range.Start)
        {
            result.Append(command[range.Start.._range.Start]);
        }
        result.Append(_text);
        if (range.End > _range.End)
        {
            result.Append(command[_range.End..range.End]);
        }
        return new Suggestion(range, result.ToString(), _tooltip);
    }

    public virtual int CompareTo(Suggestion? other)
    {
        if (other is null)
        {
            return 1;
        }
        return string.CompareOrdinal(_text, other._text);
    }

    public virtual int CompareToIgnoreCase(Suggestion b)
    {
        return string.Compare(_text, b._text, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(Suggestion? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        return _range == other._range && _text == other._text && Equals(_tooltip, other._tooltip);
    }

    public override bool Equals(object? obj) => obj is Suggestion other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = _range.GetHashCode();
            hash = (hash * 31) ^ (_text?.GetHashCode() ?? 0);
            hash = (hash * 31) ^ (_tooltip?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public override string ToString()
    {
        return "Suggestion{range=" + _range + ", text='" + _text + "', tooltip='" + _tooltip + "'}";
    }

    public static bool operator ==(Suggestion? left, Suggestion? right) => Equals(left, right);
    public static bool operator !=(Suggestion? left, Suggestion? right) => !Equals(left, right);
}
