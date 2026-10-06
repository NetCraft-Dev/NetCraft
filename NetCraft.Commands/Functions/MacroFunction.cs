using System.Globalization;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Execution;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Commands.Functions;

//MacroFunction 带宏的函数对应原版 net.minecraft.commands.functions.MacroFunction
//instantiate 按参数取值替换宏行重新解析 实例结果按参数列表缓存最多 8 份
public class MacroFunction<T> : CommandFunction<T>
{
    //DecimalFormatString 等价原版 DecimalFormat('#') 且最多 15 位小数 整数不带小数点
    private const string DecimalFormatString = "0.###############";

    //MaxCacheEntries 实例缓存上限
    private const int MaxCacheEntries = 8;

    private readonly List<string> _parameters;
    private readonly LinkedList<KeyValuePair<List<string>, InstantiatedFunction<T>>> _cache = new();
    private readonly Identifier _id;
    private readonly List<Entry<T>> _entries;

    public MacroFunction(Identifier id, List<Entry<T>> entries, List<string> parameters)
    {
        _id = id;
        _entries = entries;
        _parameters = parameters;
    }

    public Identifier Id => _id;

    //Instantiate 按参数实例化缺参直接炸对应原版 instantiate
    public InstantiatedFunction<T> Instantiate(CompoundTag? arguments, CommandDispatcher<T> dispatcher)
    {
        if (arguments is null)
        {
            throw new FunctionInstantiationException($"Function {_id} requires macro arguments but none were provided");
        }
        var parameterValues = new List<string>(_parameters.Count);
        foreach (var parameter in _parameters)
        {
            var argumentValue = arguments[parameter];
            if (argumentValue is null)
            {
                throw new FunctionInstantiationException($"Function {_id} is missing argument '{parameter}'");
            }
            parameterValues.Add(Stringify(argumentValue));
        }
        if (GetCached(parameterValues) is { } cachedFunction)
        {
            return cachedFunction;
        }
        if (_cache.Count >= MaxCacheEntries)
        {
            _cache.RemoveFirst();
        }
        var function = SubstituteAndParse(parameterValues, dispatcher);
        _cache.AddLast(new KeyValuePair<List<string>, InstantiatedFunction<T>>(parameterValues, function));
        return function;
    }

    //GetCached 取缓存命中则移到队尾
    private InstantiatedFunction<T>? GetCached(List<string> parameterValues)
    {
        foreach (var pair in _cache)
        {
            if (pair.Key.SequenceEqual(parameterValues))
            {
                _cache.Remove(pair);
                _cache.AddLast(pair);
                return pair.Value;
            }
        }
        return null;
    }

    //Stringify 参数值转文本浮点走 DECIMAL_FORMAT 对应原版 stringify
    private static string Stringify(Tag tag)
    {
        switch (tag)
        {
            case FloatTag floatTag:
                return floatTag.Value.ToString(DecimalFormatString, CultureInfo.InvariantCulture);
            case DoubleTag doubleTag:
                return doubleTag.Value.ToString(DecimalFormatString, CultureInfo.InvariantCulture);
            case ByteTag byteTag:
                return byteTag.Value.ToString();
            case ShortTag shortTag:
                return shortTag.Value.ToString();
            case LongTag longTag:
                return longTag.Value.ToString();
            case StringTag stringTag:
                return stringTag.Value;
            default:
                return tag.ToString();
        }
    }

    private InstantiatedFunction<T> SubstituteAndParse(List<string> values, CommandDispatcher<T> dispatcher)
    {
        var newEntries = new List<UnboundEntryAction<T>>(_entries.Count);
        var entryArguments = new List<string>(values.Count);
        foreach (var entry in _entries)
        {
            LookupValues(values, entry.Parameters, entryArguments);
            newEntries.Add(entry.Instantiate(entryArguments, dispatcher, _id));
        }
        return new PlainTextFunction<T>(_id.WithPath(id => id + "/" + values.GetHashCode()), newEntries);
    }

    //LookupValues 按下标选值复用输出列表对应原版 lookupValues
    private static void LookupValues(List<string> values, List<int> indicesToSelect, List<string> selectedValuesOutput)
    {
        selectedValuesOutput.Clear();
        foreach (var index in indicesToSelect)
        {
            selectedValuesOutput.Add(values[index]);
        }
    }

    //Entry 函数条目抽象
    public interface Entry<T2>
    {
        //Parameters 引用的宏参数下标
        List<int> Parameters { get; }

        //UnboundEntryAction 按实参实例化出动作
        UnboundEntryAction<T2> Instantiate(List<string> substitutions, CommandDispatcher<T2> dispatcher, Identifier functionId);
    }

    //MacroEntry 宏行条目
    public sealed class MacroEntry<T2>(
        StringTemplate template, List<int> parameters, T2 compilationContext) : Entry<T2>
    {
        public List<int> Parameters => parameters;

        public UnboundEntryAction<T2> Instantiate(List<string> substitutions, CommandDispatcher<T2> dispatcher, Identifier functionId)
        {
            var command = template.Substitute(substitutions);
            try
            {
                return CommandFunction<T2>.ParseCommand(dispatcher, compilationContext, new StringReader(command));
            }
            catch (CommandSyntaxException e)
            {
                throw new FunctionInstantiationException($"Failed to parse function {functionId} on command '{command}': {e.Message}");
            }
        }
    }

    //PlainTextEntry 已编译的普通条目
    public sealed class PlainTextEntry<T2>(UnboundEntryAction<T2> compiledAction) : Entry<T2>
    {
        public List<int> Parameters => [];

        public UnboundEntryAction<T2> Instantiate(List<string> substitutions, CommandDispatcher<T2> dispatcher, Identifier functionId)
            => compiledAction;
    }
}
