using NetCraft.Codec;
using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//Grammar, maps to vanilla net.minecraft.util.parsing.packrat.commands.Grammar
//Wraps Dictionary and the top-level NamedRule, provides the parseForCommands entry
//parseForSuggestions depends on the brigadier suggestion system, not yet ported and throws NotSupportedException
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

    //parseForCommands parses from CommandStringReader, throws CommandSyntaxException on failure
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
