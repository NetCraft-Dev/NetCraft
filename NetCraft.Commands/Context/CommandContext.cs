using NetCraft.Commands.Execution;
using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Context;

//CommandContext maps to vanilla com.mojang.brigadier.context.CommandContext
//Holds source/input/command/arguments/nodes/range/child/modifier/forks
//getArgument checks the type against PRIMITIVE_TO_WRAPPER; in C# a value type maps to itself, kept to align with Java semantics
public sealed class CommandContext<S>
{
    //PRIMITIVE_TO_WRAPPER in C# a value type maps to itself; kept only as a placeholder aligning with the Java design
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
    private readonly CustomCommandExecutor<S>? _customExecutor;

    public CommandContext(S source, string input, Dictionary<string, ParsedArgument<S>> arguments, Command<S>? command, CommandNode<S> rootNode, List<ParsedCommandNode<S>> nodes, StringRange range, CommandContext<S>? child, RedirectModifier<S>? modifier, bool forks, CustomCommandExecutor<S>? customExecutor = null)
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
        _customExecutor = customExecutor;
    }

    public CommandContext<S> CopyFor(S source)
    {
        if (EqualityComparer<S>.Default.Equals(_source, source))
        {
            return this;
        }
        return new CommandContext<S>(source, _input, _arguments, _command, _rootNode, _nodes, _range, _child, _modifier, _forks, _customExecutor);
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

    //CustomExecutor custom executor; once BuildContexts detects it, execution bypasses the ordinary command delegate path
    public CustomCommandExecutor<S>? CustomExecutor => _customExecutor;

    public S GetSource() => _source;

    public V GetArgument<V>(string name)
    {
        return GetArgument<V>(name, typeof(V));
    }

    //GetArgument looks up the ParsedArgument by name, checks the type is assignable and returns it
    //The clazz parameter aligns with Java's Class<V> semantics and is effectively equivalent to typeof(V)
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
