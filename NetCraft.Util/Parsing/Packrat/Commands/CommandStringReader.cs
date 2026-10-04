using System.Text;

namespace NetCraft.Util;

//CommandStringReader 字符串读取器对应原版 brigadier StringReader
//提供游标式解析字符串的支持方法
//改名 CommandStringReader 避免与 System.IO.StringReader 冲突
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
        if (!CanRead()) throw new InvalidOperationException("无字符可读");
        return _str[_cursor];
    }

    public char Peek(int offset)
    {
        if (!CanRead(offset + 1)) throw new InvalidOperationException("无字符可读");
        return _str[_cursor + offset];
    }

    public char Read()
    {
        if (!CanRead()) throw new InvalidOperationException("无字符可读");
        return _str[_cursor++];
    }

    public void Skip()
    {
        if (!CanRead()) throw new InvalidOperationException("无字符可读");
        _cursor++;
    }

    public void SkipWhitespace()
    {
        while (CanRead() && char.IsWhiteSpace(_str[_cursor]))
        {
            _cursor++;
        }
    }

    //ReadString 读带引号或不带引号字符串
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

    //ReadUnquotedString 读isAllowedInUnquotedString允许的字符序列
    public string ReadUnquotedString()
    {
        var start = _cursor;
        while (CanRead() && IsAllowedInUnquotedString(Peek()))
        {
            _cursor++;
        }
        return _str[start.._cursor];
    }

    //isAllowedInUnquotedString对应原版brigadier StringReader.isAllowedInUnquotedString
    //只允许字母数字下划线减号小数点加号其他字符停止读取
    public static bool IsAllowedInUnquotedString(char c)
        => c is (>= '0' and <= '9')
            or (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z')
            or '_' or '-' or '.' or '+';

    //ReadQuotedString 读引号包围的字符串支持转义
    public string ReadQuotedString()
    {
        if (!CanRead()) return string.Empty;
        var quote = Read();
        if (quote != '"' && quote != '\'')
        {
            throw new InvalidOperationException("预期引号");
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
                    throw new InvalidOperationException($"无效转义字符 {c}");
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
        throw new InvalidOperationException("字符串未闭合");
    }

    //ReadInt 读 int 数字支持负号
    public int ReadInt()
    {
        var s = ReadNumberToken();
        if (!int.TryParse(s, out var value)) throw new InvalidOperationException($"无效 int {s}");
        return value;
    }

    //ReadLong 读 long 数字支持负号
    public long ReadLong()
    {
        var s = ReadNumberToken();
        if (!long.TryParse(s, out var value)) throw new InvalidOperationException($"无效 long {s}");
        return value;
    }

    //ReadFloat 读 float 数字支持负号小数指数
    public float ReadFloat()
    {
        var s = ReadNumberToken();
        if (!float.TryParse(s, out var value)) throw new InvalidOperationException($"无效 float {s}");
        return value;
    }

    //ReadDouble 读 double 数字支持负号小数指数
    public double ReadDouble()
    {
        var s = ReadNumberToken();
        if (!double.TryParse(s, out var value)) throw new InvalidOperationException($"无效 double {s}");
        return value;
    }

    //ReadNumberToken 读连续数字字符支持负号小数点指数符号
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
        if (_cursor == start) throw new InvalidOperationException("无数字可读");
        return _str[start.._cursor];
    }
}
