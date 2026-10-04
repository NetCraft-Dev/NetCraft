using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//无引号字符串匹配规则对应原版net.minecraft.util.parsing.packrat.commands.UnquotedStringParseRule
//调用CommandStringReader.ReadUnquotedString长度不足minSize失败
public sealed class UnquotedStringParseRule : Rule<CommandStringReader, string>
{
    private readonly int _minSize;
    private readonly DelayedException<CommandSyntaxException> _error;

    public UnquotedStringParseRule(int minSize, DelayedException<CommandSyntaxException> error)
    {
        _minSize = minSize;
        _error = error;
    }

    public string Parse(ParseState<CommandStringReader> state)
    {
        state.Input.SkipWhitespace();
        var cursor = state.Mark();
        var value = state.Input.ReadUnquotedString();
        if (value.Length < _minSize)
        {
            state.ErrorCollector.Store(cursor, _error);
            return null!;
        }
        return value;
    }
}
