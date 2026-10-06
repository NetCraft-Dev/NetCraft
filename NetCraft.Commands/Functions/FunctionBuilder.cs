using NetCraft.Commands.Execution;
using NetCraft.Registry;

namespace NetCraft.Commands.Functions;

//FunctionBuilder 函数编译器对应原版 net.minecraft.commands.functions.FunctionBuilder
//收集命令条目与宏模板 出现宏时整条函数升级为 MacroFunction
//原版是包私有类 这里 internal
internal class FunctionBuilder<T>
{
    private List<UnboundEntryAction<T>>? _plainEntries = [];
    private List<MacroFunction<T>.Entry<T>>? _macroEntries;
    private readonly List<string> _macroArguments = [];

    //AddCommand 追加一条编译好的命令
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

    //GetArgumentIndex 宏参数名转下标 去重
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

    //AddMacro 追加一条宏行触发整函数升级为宏函数
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

    //Build 收尾产出函数
    public CommandFunction<T> Build(Identifier id)
    {
        if (_macroEntries is not null)
        {
            return new MacroFunction<T>(id, _macroEntries, _macroArguments);
        }
        return new PlainTextFunction<T>(id, _plainEntries ?? []);
    }
}
