using NetCraft.Commands.Execution;
using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Context;

//CommandContextBuilder maps to vanilla com.mojang.brigadier.context.CommandContextBuilder
//Accumulates arguments/nodes/range/child/modifier/forks during parsing and finally builds a CommandContext
public sealed class CommandContextBuilder<S>
{
    private readonly Dictionary<string, ParsedArgument<S>> _arguments = new();
    private readonly CommandNode<S> _rootNode;
    private readonly List<ParsedCommandNode<S>> _nodes = new();
    private readonly CommandDispatcher<S> _dispatcher;
    private S _source;
    private Command<S>? _command;
    private CustomCommandExecutor<S>? _customExecutor;
    private CommandContextBuilder<S>? _child;
    private StringRange _range;
    private RedirectModifier<S>? _modifier;
    private bool _forks;

    public CommandContextBuilder(CommandDispatcher<S> dispatcher, S source, CommandNode<S> rootNode, int start)
    {
        _rootNode = rootNode;
        _dispatcher = dispatcher;
        _source = source;
        _range = StringRange.At(start);
    }

    public CommandContextBuilder<S> WithSource(S source)
    {
        _source = source;
        return this;
    }

    public S GetSource() => _source;

    public CommandNode<S> GetRootNode() => _rootNode;

    public CommandContextBuilder<S> WithArgument(string name, ParsedArgument<S> argument)
    {
        _arguments[name] = argument;
        return this;
    }

    public IReadOnlyDictionary<string, ParsedArgument<S>> GetArguments() => _arguments;

    public CommandContextBuilder<S> WithCommand(Command<S>? command)
    {
        _command = command;
        return this;
    }

    //WithCustomExecutor registers a custom executor, mirroring the dual role of CommandAdapter on a vanilla command node
    //A delegate cannot implement an interface, so the command slot gets a placeholder delegate and the custom executor is stored separately
    public CommandContextBuilder<S> WithCustomExecutor(CustomCommandExecutor<S> executor)
    {
        _command = CustomCommandExecutor<S>.NotExecutable;
        _customExecutor = executor;
        return this;
    }

    public CustomCommandExecutor<S>? GetCustomExecutor() => _customExecutor;

    public CommandContextBuilder<S> WithNode(CommandNode<S> node, StringRange range)
    {
        _nodes.Add(new ParsedCommandNode<S>(node, range));
        _range = StringRange.Encompassing(_range, range);
        _modifier = node.GetRedirectModifier();
        _forks = node.IsFork();
        return this;
    }

    public CommandContextBuilder<S> Copy()
    {
        var copy = new CommandContextBuilder<S>(_dispatcher, _source, _rootNode, _range.Start);
        copy._command = _command;
        copy._customExecutor = _customExecutor;
        foreach (var (k, v) in _arguments)
        {
            copy._arguments[k] = v;
        }
        copy._nodes.AddRange(_nodes);
        copy._child = _child;
        copy._range = _range;
        copy._forks = _forks;
        return copy;
    }

    public CommandContextBuilder<S> WithChild(CommandContextBuilder<S>? child)
    {
        _child = child;
        return this;
    }

    public CommandContextBuilder<S>? GetChild() => _child;

    public CommandContextBuilder<S> GetLastChild()
    {
        var result = this;
        while (result._child != null)
        {
            result = result._child;
        }
        return result;
    }

    public Command<S>? GetCommand() => _command;

    public IReadOnlyList<ParsedCommandNode<S>> GetNodes() => _nodes;

    public CommandContext<S> Build(string input)
    {
        return new CommandContext<S>(_source, input, _arguments, _command, _rootNode, _nodes, _range, _child?.Build(input), _modifier, _forks, _customExecutor);
    }

    public CommandDispatcher<S> GetDispatcher() => _dispatcher;

    public StringRange GetRange() => _range;

    public SuggestionContext<S> FindSuggestionContext(int cursor)
    {
        if (_range.Start <= cursor)
        {
            if (_range.End < cursor)
            {
                if (_child != null)
                {
                    return _child.FindSuggestionContext(cursor);
                }
                if (_nodes.Count > 0)
                {
                    var last = _nodes[_nodes.Count - 1];
                    return new SuggestionContext<S>(this, last.Node, last.Range.End + 1);
                }
                return new SuggestionContext<S>(this, _rootNode, _range.Start);
            }

            CommandNode<S>? prev = _rootNode;
            foreach (var node in _nodes)
            {
                var nodeRange = node.Range;
                if (nodeRange.Start <= cursor && cursor <= nodeRange.End)
                {
                    return new SuggestionContext<S>(this, prev, nodeRange.Start);
                }
                prev = node.Node;
            }
            if (prev == null)
            {
                throw new InvalidOperationException("Can't find node before cursor");
            }
            return new SuggestionContext<S>(this, prev, _range.Start);
        }
        throw new InvalidOperationException("Can't find node before cursor");
    }
}
