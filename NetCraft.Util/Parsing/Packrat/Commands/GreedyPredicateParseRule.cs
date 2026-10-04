using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//贪婪谓词匹配规则对应原版net.minecraft.util.parsing.packrat.commands.GreedyPredicateParseRule
//按字符谓词连续匹配minSize到maxSize长度失败返回null
public abstract class GreedyPredicateParseRule : Rule<CommandStringReader, string>
{
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly DelayedException<CommandSyntaxException> _error;

    protected abstract bool IsAccepted(char c);

    protected GreedyPredicateParseRule(int minSize, DelayedException<CommandSyntaxException> error)
        : this(minSize, int.MaxValue, error) { }

    protected GreedyPredicateParseRule(int minSize, int maxSize, DelayedException<CommandSyntaxException> error)
    {
        _minSize = minSize;
        _maxSize = maxSize;
        _error = error;
    }

    public string Parse(ParseState<CommandStringReader> state)
    {
        var input = state.Input;
        var fullString = input.String;
        var start = input.Cursor;
        var pos = start;
        while (pos < fullString.Length && IsAccepted(fullString[pos]) && pos - start < _maxSize)
        {
            pos++;
        }
        var length = pos - start;
        if (length < _minSize)
        {
            state.ErrorCollector.Store(state.Mark(), _error);
            return null!;
        }
        input.Cursor = pos;
        return fullString.Substring(start, length);
    }
}
