using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Context;

//CommandContext 命令上下文对应原版com.mojang.brigadier.context.CommandContext
//持source/input/command/arguments/nodes/range/child/modifier/forks
//getArgument按类型校验PRIMITIVE_TO_WRAPPER在C#值类型即类型本身保留以对齐Java语义
public sealed class CommandContext<S>
{
    //PRIMITIVE_TO_WRAPPER 在C#值类型即类型自身映射仅占位对齐Java设计
    private static readonly Dictionary<Type, Type> _primitiveToWrapper = new()
    {
        { typeof(bool), typeof(bool) },
        { typeof(byte), typeof(byte) },
        { typeof(sbyte), typeof(sbyte) },
        { typeof(short), typeof(short) },
        { typeof(ushort), typeof(ushort) },
        { typeof(char), typeof(char) },
        { typeof(int), typeof(int) },
        { typeof(uint), typeof(uint) },
        { typeof(long), typeof(long) },
        { typeof(ulong), typeof(ulong) },
        { typeof(float), typeof(float) },
        { typeof(double), typeof(double) },
    };

    private readonly S _source;
    private readonly string _input;
    private readonly Command<S>? _command;
    private readonly Dictionary<string, ParsedArgument<S>> _arguments;
    private readonly CommandNode<S> _rootNode;
    private readonly List<ParsedCommandNode<S>> _nodes;
    private readonly StringRange _range;
    private readonly CommandContext<S>? _child;
    private readonly RedirectModifier<S>? _modifier;
    private readonly bool _forks;

    public CommandContext(S source, string input, Dictionary<string, ParsedArgument<S>> arguments, Command<S>? command, CommandNode<S> rootNode, List<ParsedCommandNode<S>> nodes, StringRange range, CommandContext<S>? child, RedirectModifier<S>? modifier, bool forks)
    {
        _source = source;
        _input = input;
        _arguments = arguments;
        _command = command;
        _rootNode = rootNode;
        _nodes = nodes;
        _range = range;
        _child = child;
        _modifier = modifier;
        _forks = forks;
    }

    public CommandContext<S> CopyFor(S source)
    {
        if (EqualityComparer<S>.Default.Equals(_source, source))
        {
            return this;
        }
        return new CommandContext<S>(source, _input, _arguments, _command, _rootNode, _nodes, _range, _child, _modifier, _forks);
    }

    public CommandContext<S>? GetChild() => _child;

    public CommandContext<S> GetLastChild()
    {
        var result = this;
        while (result._child != null)
        {
            result = result._child;
        }
        return result;
    }

    public Command<S>? GetCommand() => _command;

    public S GetSource() => _source;

    public V GetArgument<V>(string name)
    {
        return GetArgument<V>(name, typeof(V));
    }

    //GetArgument 按name取出ParsedArgument校验类型可赋值后返回
    //clazz参数对齐Java Class<V>语义实际与typeof(V)等价
    public V GetArgument<V>(string name, Type clazz)
    {
        if (!_arguments.TryGetValue(name, out var argument))
        {
            throw new ArgumentException("No such argument '" + name + "' exists on this command");
        }

        var result = argument.GetResult();
        var wrapper = _primitiveToWrapper.TryGetValue(clazz, out var w) ? w : clazz;
        if (result != null && wrapper.IsAssignableFrom(result.GetType()))
        {
            return (V)result;
        }
        throw new ArgumentException("Argument '" + name + "' is defined as " + (result?.GetType().Name ?? "null") + ", not " + clazz.Name);
    }

    public override bool Equals(object? o)
    {
        if (ReferenceEquals(this, o)) return true;
        if (o is not CommandContext<S> that) return false;

        if (!DictionaryEquals(_arguments, that._arguments)) return false;
        if (!_rootNode.Equals(that._rootNode)) return false;
        if (_nodes.Count != that._nodes.Count || !_nodes.SequenceEqual(that._nodes)) return false;
        if (!ReferenceEquals(_command, that._command)) return false;
        if (!EqualityComparer<S>.Default.Equals(_source, that._source)) return false;
        if (_child != null ? !_child.Equals(that._child) : that._child != null) return false;

        return true;
    }

    private static bool DictionaryEquals(Dictionary<string, ParsedArgument<S>> a, Dictionary<string, ParsedArgument<S>> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (k, v) in a)
        {
            if (!b.TryGetValue(k, out var bv) || !Equals(v, bv)) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var result = EqualityComparer<S>.Default.GetHashCode(_source!);
        result = 31 * result + _arguments.GetHashCode();
        result = 31 * result + (_command != null ? _command.GetHashCode() : 0);
        result = 31 * result + _rootNode.GetHashCode();
        result = 31 * result + _nodes.GetHashCode();
        result = 31 * result + (_child != null ? _child.GetHashCode() : 0);
        return result;
    }

    public RedirectModifier<S>? GetRedirectModifier() => _modifier;

    public StringRange GetRange() => _range;

    public string GetInput() => _input;

    public CommandNode<S> GetRootNode() => _rootNode;

    public IReadOnlyList<ParsedCommandNode<S>> GetNodes() => _nodes;

    public bool HasNodes() => _nodes.Count > 0;

    public bool IsForked() => _forks;
}
