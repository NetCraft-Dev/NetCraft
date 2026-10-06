using System.Text;

namespace NetCraft.Util;

//CommandStringReader string reader, maps to vanilla brigadier StringReader
//Provides cursor-style helpers for parsing strings
//Renamed to CommandStringReader to avoid clashing with System.IO.StringReader
public sealed class CommandStringReader
{
    private readonly string _str;
    private int _cursor;

    public CommandStringReader(string str)
    {
        _str = str;
    }

    public string String => _str;
    public int Cursor
    {
        get => _cursor;
        set
        {
            if (value < 0 || value > _str.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            _cursor = value;
        }
    }
    public int RemainingLength => _str.Length - _cursor;
    public int TotalLength => _str.Length;
    public bool CanRead(int length) => _cursor + length <= _str.Length;
    public bool CanRead() => CanRead(1);

    public char Peek()
    {
        if (!CanRead()) throw new InvalidOperationException("No characters to read");
        return _str[_cursor];
    }

    public char Peek(int offset)
    {
        if (!CanRead(offset + 1)) throw new InvalidOperationException("No characters to read");
        return _str[_cursor + offset];
    }

    public char Read()
    {
        if (!CanRead()) throw new InvalidOperationException("No characters to read");
        return _str[_cursor++];
    }

    public void Skip()
    {
        if (!CanRead()) throw new InvalidOperationException("No characters to read");
        _cursor++;
    }

    public void SkipWhitespace()
    {
        while (CanRead() && char.IsWhiteSpace(_str[_cursor]))
        {
            _cursor++;
        }
    }

    //ReadString reads a quoted or unquoted string
    public string ReadString()
    {
        if (!CanRead()) return string.Empty;
        var next = Peek();
        if (next == '"' || next == '\'')
        {
            return ReadQuotedString();
        }
        return ReadUnquotedString();
    }

    //ReadUnquotedString reads the character sequence allowed by isAllowedInUnquotedString
    public string ReadUnquotedString()
    {
        var start = _cursor;
        while (CanRead() && IsAllowedInUnquotedString(Peek()))
        {
            _cursor++;
        }
        return _str[start.._cursor];
    }

    //isAllowedInUnquotedString maps to vanilla brigadier StringReader.isAllowedInUnquotedString
    //Only allows letters, digits, underscore, minus, dot and plus, stopping at any other character
    public static bool IsAllowedInUnquotedString(char c)
        => c is (>= '0' and <= '9')
            or (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z')
            or '_' or '-' or '.' or '+';

    //ReadQuotedString reads a quote-enclosed string, supports escapes
    public string ReadQuotedString()
    {
        if (!CanRead()) return string.Empty;
        var quote = Read();
        if (quote != '"' && quote != '\'')
        {
            throw new InvalidOperationException("Expected quote");
        }
        var result = new StringBuilder();
        bool escaped = false;
        while (CanRead())
        {
            var c = Read();
            if (escaped)
            {
                if (c == '"' || c == '\'' || c == '\\')
                {
                    result.Append(c);
                    escaped = false;
                }
                else
                {
                    throw new InvalidOperationException($"Invalid escape character {c}");
                }
            }
            else if (c == '\\')
            {
                escaped = true;
            }
            else if (c == quote)
            {
                return result.ToString();
            }
            else
            {
                result.Append(c);
            }
        }
        throw new InvalidOperationException("Unterminated string");
    }

    //ReadInt reads an int number, supports a leading minus
    public int ReadInt()
    {
        var s = ReadNumberToken();
        if (!int.TryParse(s, out var value)) throw new InvalidOperationException($"Invalid int {s}");
        return value;
    }

    //ReadLong reads a long number, supports a leading minus
    public long ReadLong()
    {
        var s = ReadNumberToken();
        if (!long.TryParse(s, out var value)) throw new InvalidOperationException($"Invalid long {s}");
        return value;
    }

    //ReadFloat reads a float number, supports a leading minus, decimal point and exponent
    public float ReadFloat()
    {
        var s = ReadNumberToken();
        if (!float.TryParse(s, out var value)) throw new InvalidOperationException($"Invalid float {s}");
        return value;
    }

    //ReadDouble reads a double number, supports a leading minus, decimal point and exponent
    public double ReadDouble()
    {
        var s = ReadNumberToken();
        if (!double.TryParse(s, out var value)) throw new InvalidOperationException($"Invalid double {s}");
        return value;
    }

    //ReadNumberToken reads consecutive numeric characters, supports minus, dot and exponent sign
    private string ReadNumberToken()
    {
        var start = _cursor;
        while (CanRead())
        {
            var c = Peek();
            if (char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
            {
                _cursor++;
            }
            else
            {
                break;
            }
        }
        if (_cursor == start) throw new InvalidOperationException("No digits to read");
        return _str[start.._cursor];
    }
}
