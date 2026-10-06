using NetCraft.Commands.Execution;
using NetCraft.Registry;

namespace NetCraft.Commands.Functions;

//FunctionBuilder function compiler, maps to vanilla net.minecraft.commands.functions.FunctionBuilder
//Collects command entries and macro templates; the whole function upgrades to a MacroFunction once a macro appears
//Package-private in vanilla, internal here
internal class FunctionBuilder<T>
{
    private List<UnboundEntryAction<T>>? _plainEntries = [];
    private List<MacroFunction<T>.Entry<T>>? _macroEntries;
    private readonly List<string> _macroArguments = [];

    //AddCommand appends a compiled command
    public void AddCommand(UnboundEntryAction<T> command)
    {
        if (_macroEntries is not null)
        {
            _macroEntries.Add(new MacroFunction<T>.PlainTextEntry<T>(command));
        }
        else
        {
            _plainEntries!.Add(command);
        }
    }

    //GetArgumentIndex maps a macro argument name to an index, deduplicating
    private int GetArgumentIndex(string id)
    {
        var index = _macroArguments.IndexOf(id);
        if (index == -1)
        {
            index = _macroArguments.Count;
            _macroArguments.Add(id);
        }
        return index;
    }

    private List<int> ConvertToIndices(List<string> ids)
        => ids.Select(GetArgumentIndex).ToList();

    //AddMacro appends a macro line, triggering the whole function to upgrade to a macro function
    public void AddMacro(string command, int line, T compilationContext)
    {
        StringTemplate parseResults;
        try
        {
            parseResults = StringTemplate.FromString(command);
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Can't parse function line {line}: '{command}'", e);
        }
        if (_plainEntries is not null)
        {
            _macroEntries = new List<MacroFunction<T>.Entry<T>>(_plainEntries.Count + 1);
            foreach (var plainEntry in _plainEntries)
            {
                _macroEntries.Add(new MacroFunction<T>.PlainTextEntry<T>(plainEntry));
            }
            _plainEntries = null;
        }
        _macroEntries!.Add(new MacroFunction<T>.MacroEntry<T>(parseResults, ConvertToIndices(parseResults.Variables), compilationContext));
    }

    //Build finishes and produces the function
    public CommandFunction<T> Build(Identifier id)
    {
        if (_macroEntries is not null)
        {
            return new MacroFunction<T>(id, _macroEntries, _macroArguments);
        }
        return new PlainTextFunction<T>(id, _plainEntries ?? []);
    }
}
