using NetCraft.Codec;
using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//语法对应原版net.minecraft.util.parsing.packrat.commands.Grammar
//封装Dictionary和顶层NamedRule提供parseForCommands入口
//parseForSuggestions依赖brigadier建议系统暂未移植抛NotSupportedException
public sealed class Grammar<T>
{
    public Dictionary<CommandStringReader> Rules { get; }
    public NamedRule<CommandStringReader, T> Top { get; }

    public Grammar(Dictionary<CommandStringReader> rules, NamedRule<CommandStringReader, T> top)
    {
        rules.CheckAllBound();
        Rules = rules;
        Top = top;
    }

    public Optional<T> Parse(ParseState<CommandStringReader> state)
        => state.ParseTopRule(Top);

    //parseForCommands从CommandStringReader解析失败抛CommandSyntaxException
    public T ParseForCommands(CommandStringReader reader)
    {
        var longestOnly = new LongestOnlyErrorCollector<CommandStringReader>();
        var optional = Parse(new StringReaderParserState(longestOnly, reader));
        if (optional.IsPresent)
        {
            return optional.Get();
        }
        var listEntries = longestOnly.Entries();
        var exceptions = new List<Exception>();
        foreach (var entry in listEntries)
        {
            if (entry.Reason is DelayedException<CommandSyntaxException> delayed)
            {
                exceptions.Add(delayed(reader.String, entry.Cursor));
            }
            else if (entry.Reason is Exception ex)
            {
                exceptions.Add(ex);
            }
        }
        foreach (var ex in exceptions)
        {
            if (ex is CommandSyntaxException cse) throw cse;
        }
        if (exceptions.Count >= 1) throw exceptions[0];
        throw new InvalidOperationException("Failed to parse: " + string.Join(", ", listEntries));
    }

    public object ParseForSuggestions(object suggestionsBuilder)
        => throw new NotSupportedException("Suggestions not implemented");
}
