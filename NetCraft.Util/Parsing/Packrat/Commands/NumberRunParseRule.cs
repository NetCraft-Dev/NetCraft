using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//Number run parse rule, maps to vanilla net.minecraft.util.parsing.packrat.commands.NumberRunParseRule
//Matches consecutively by a character predicate, no underscore at either end
public abstract class NumberRunParseRule : Rule<CommandStringReader, string>
{
    private readonly DelayedException<CommandSyntaxException> _noValueError;
    private readonly DelayedException<CommandSyntaxException> _underscoreNotAllowedError;

    protected abstract bool IsAccepted(char c);

    protected NumberRunParseRule(
        DelayedException<CommandSyntaxException> noValueError,
        DelayedException<CommandSyntaxException> underscoreNotAllowedError)
    {
        _noValueError = noValueError;
        _underscoreNotAllowedError = underscoreNotAllowedError;
    }

    public string Parse(ParseState<CommandStringReader> state)
    {
        var input = state.Input;
        input.SkipWhitespace();
        var fullString = input.String;
        var start = input.Cursor;
        var pos = start;
        while (pos < fullString.Length && IsAccepted(fullString[pos]))
        {
            pos++;
        }
        var length = pos - start;
        if (length == 0)
        {
            state.ErrorCollector.Store(state.Mark(), _noValueError);
            return null!;
        }
        if (fullString[start] == '_' || fullString[pos - 1] == '_')
        {
            state.ErrorCollector.Store(state.Mark(), _underscoreNotAllowedError);
            return null!;
        }
        input.Cursor = pos;
        return fullString.Substring(start, length);
    }
}
