using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//Unquoted string parse rule, maps to vanilla net.minecraft.util.parsing.packrat.commands.UnquotedStringParseRule
//Calls CommandStringReader.ReadUnquotedString, fails if shorter than minSize
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
