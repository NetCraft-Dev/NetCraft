using System.Text.RegularExpressions;
using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//Greedy regex parse rule, maps to vanilla net.minecraft.util.parsing.packrat.commands.GreedyPatternParseRule
//Uses Regex to lookingAt match from the cursor position, returns null on failure
public sealed class GreedyPatternParseRule : Rule<CommandStringReader, string>
{
    private readonly Regex _pattern;
    private readonly DelayedException<CommandSyntaxException> _error;

    public GreedyPatternParseRule(Regex pattern, DelayedException<CommandSyntaxException> error)
    {
        _pattern = pattern;
        _error = error;
    }

    public string Parse(ParseState<CommandStringReader> state)
    {
        var input = state.Input;
        var fullString = input.String;
        var start = input.Cursor;
        var match = _pattern.Match(fullString, start);
        if (!match.Success || match.Index != start)
        {
            state.ErrorCollector.Store(state.Mark(), _error);
            return null!;
        }
        input.Cursor = match.Index + match.Length;
        return match.Value;
    }
}
