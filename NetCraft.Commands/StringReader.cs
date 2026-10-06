using System.Text;

using NetCraft.Commands.Exceptions;

namespace NetCraft.Commands;

//StringReader string reader, maps to vanilla com.mojang.brigadier.StringReader
//Cursor-based string parsing; every numeric read throws a CommandSyntaxException carrying cursor context
public sealed class StringReader : IImmutableStringReader
{
    public const char SYNTAX_ESCAPE = '\\';
    public const char SYNTAX_DOUBLE_QUOTE = '"';
    public const char SYNTAX_SINGLE_QUOTE = '\'';

    private readonly string _string;
    private int _cursor;

    public StringReader(StringReader other)
    {
        _string = other._string;
        _cursor = other._cursor;
    }

    public StringReader(string @string)
    {
        _string = @string;
    }

    public string String => _string;

    public void SetCursor(int cursor) => _cursor = cursor;

    public int RemainingLength => _string.Length - _cursor;

    public int TotalLength => _string.Length;

    public int Cursor => _cursor;

    public string GetRead() => _string[.._cursor];

    public string Remaining => _string[_cursor..];

    public bool CanRead(int length) => _cursor + length <= _string.Length;

    public bool CanRead() => CanRead(1);

    public char Peek() => _string[_cursor];

    public char Peek(int offset) => _string[_cursor + offset];

    public char Read() => _string[_cursor++];

    public void Skip() => _cursor++;

    //IsAllowedNumber numeric characters allow 0-9, the decimal point and a minus sign
    public static bool IsAllowedNumber(char c) => c is >= '0' and <= '9' or '.' or '-';

    public static bool IsQuotedStringStart(char c) => c == SYNTAX_DOUBLE_QUOTE || c == SYNTAX_SINGLE_QUOTE;

    public void SkipWhitespace()
    {
        while (CanRead() && char.IsWhiteSpace(Peek()))
        {
            Skip();
        }
    }

    public int ReadInt()
    {
        var start = _cursor;
        while (CanRead() && IsAllowedNumber(Peek()))
        {
            Skip();
        }
        var number = _string[start.._cursor];
        if (number.Length == 0)
        {
            throw CommandSyntaxException.BuiltInExceptions.ReaderExpectedInt().CreateWithContext(this);
        }
        try
        {
            return int.Parse(number);
        }
        catch (FormatException)
        {
            _cursor = start;
            throw CommandSyntaxException.BuiltInExceptions.ReaderInvalidInt().CreateWithContext(this, number);
        }
    }

    public long ReadLong()
    {
        var start = _cursor;
        while (CanRead() && IsAllowedNumber(Peek()))
        {
            Skip();
        }
        var number = _string[start.._cursor];
        if (number.Length == 0)
        {
            throw CommandSyntaxException.BuiltInExceptions.ReaderExpectedLong().CreateWithContext(this);
        }
        try
        {
            return long.Parse(number);
        }
        catch (FormatException)
        {
            _cursor = start;
            throw CommandSyntaxException.BuiltInExceptions.ReaderInvalidLong().CreateWithContext(this, number);
        }
    }

    public double ReadDouble()
    {
        var start = _cursor;
        while (CanRead() && IsAllowedNumber(Peek()))
        {
            Skip();
        }
        var number = _string[start.._cursor];
        if (number.Length == 0)
        {
            throw CommandSyntaxException.BuiltInExceptions.ReaderExpectedDouble().CreateWithContext(this);
        }
        try
        {
            return double.Parse(number);
        }
        catch (FormatException)
        {
            _cursor = start;
            throw CommandSyntaxException.BuiltInExceptions.ReaderInvalidDouble().CreateWithContext(this, number);
        }
    }

    public float ReadFloat()
    {
        var start = _cursor;
        while (CanRead() && IsAllowedNumber(Peek()))
        {
            Skip();
        }
        var number = _string[start.._cursor];
        if (number.Length == 0)
        {
            throw CommandSyntaxException.BuiltInExceptions.ReaderExpectedFloat().CreateWithContext(this);
        }
        try
        {
            return float.Parse(number);
        }
        catch (FormatException)
        {
            _cursor = start;
            throw CommandSyntaxException.BuiltInExceptions.ReaderInvalidFloat().CreateWithContext(this, number);
        }
    }

    //IsAllowedInUnquotedString unquoted strings allow 0-9, A-Z, a-z, underscore, minus, decimal point and plus
    public static bool IsAllowedInUnquotedString(char c)
        => c is (>= '0' and <= '9')
            or (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z')
            or '_' or '-' or '.' or '+';

    public string ReadUnquotedString()
    {
        var start = _cursor;
        while (CanRead() && IsAllowedInUnquotedString(Peek()))
        {
            Skip();
        }
        return _string[start.._cursor];
    }

    public string ReadQuotedString()
    {
        if (!CanRead())
        {
            return "";
        }
        var next = Peek();
        if (!IsQuotedStringStart(next))
        {
            throw CommandSyntaxException.BuiltInExceptions.ReaderExpectedStartOfQuote().CreateWithContext(this);
        }
        Skip();
        return ReadStringUntil(next);
    }

    public string ReadStringUntil(char terminator)
    {
        var result = new StringBuilder();
        var escaped = false;
        while (CanRead())
        {
            var c = Read();
            if (escaped)
            {
                if (c == terminator || c == SYNTAX_ESCAPE)
                {
                    result.Append(c);
                    escaped = false;
                }
                else
                {
                    _cursor = _cursor - 1;
                    throw CommandSyntaxException.BuiltInExceptions.ReaderInvalidEscape().CreateWithContext(this, c.ToString());
                }
            }
            else if (c == SYNTAX_ESCAPE)
            {
                escaped = true;
            }
            else if (c == terminator)
            {
                return result.ToString();
            }
            else
            {
                result.Append(c);
            }
        }
        throw CommandSyntaxException.BuiltInExceptions.ReaderExpectedEndOfQuote().CreateWithContext(this);
    }

    public string ReadString()
    {
        if (!CanRead())
        {
            return "";
        }
        var next = Peek();
        if (IsQuotedStringStart(next))
        {
            Skip();
            return ReadStringUntil(next);
        }
        return ReadUnquotedString();
    }

    public bool ReadBoolean()
    {
        var start = _cursor;
        var value = ReadString();
        if (value.Length == 0)
        {
            throw CommandSyntaxException.BuiltInExceptions.ReaderExpectedBool().CreateWithContext(this);
        }
        if (value == "true")
        {
            return true;
        }
        if (value == "false")
        {
            return false;
        }
        _cursor = start;
        throw CommandSyntaxException.BuiltInExceptions.ReaderInvalidBool().CreateWithContext(this, value);
    }

    public void Expect(char c)
    {
        if (!CanRead() || Peek() != c)
        {
            throw CommandSyntaxException.BuiltInExceptions.ReaderExpectedSymbol().CreateWithContext(this, c.ToString());
        }
        Skip();
    }
}
