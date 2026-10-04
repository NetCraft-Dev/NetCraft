using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Suggestion;

namespace NetCraft.Commands.Tree;

//CommandNode 命令节点抽象基类对应原版com.mojang.brigadier.tree.CommandNode
//持children/literals/arguments三个索引requirement/redirect/modifier/forks/command字段
//addChild按节点类型分流到literals/arguments索引合并同名节点
//getRelevantNodes临时改cursor读literal文本后恢复优化定位
public abstract class CommandNode<S> : IComparable<CommandNode<S>>
{
    private readonly Dictionary<string, CommandNode<S>> _children = new();
    private readonly Dictionary<string, LiteralCommandNode<S>> _literals = new();
    private readonly Dictionary<string, ArgumentCommandNode<S>> _arguments = new();
    private readonly Predicate<S> _requirement;
    private readonly CommandNode<S>? _redirect;
    private readonly RedirectModifier<S>? _modifier;
    private readonly bool _forks;
    private Command<S>? _command;

    protected CommandNode(Command<S>? command, Predicate<S> requirement, CommandNode<S>? redirect, RedirectModifier<S>? modifier, bool forks)
    {
        _command = command;
        _requirement = requirement;
        _redirect = redirect;
        _modifier = modifier;
        _forks = forks;
    }

    public Command<S>? GetCommand() => _command;

    public IReadOnlyCollection<CommandNode<S>> GetChildren() => _children.Values;

    public CommandNode<S>? GetChild(string name) => _children.TryGetValue(name, out var child) ? child : null;

    public CommandNode<S>? GetRedirect() => _redirect;

    public RedirectModifier<S>? GetRedirectModifier() => _modifier;

    public bool CanUse(S source) => _requirement(source);

    public void AddChild(CommandNode<S> node)
    {
        if (node is RootCommandNode<S>)
        {
            throw new NotSupportedException("Cannot add a RootCommandNode as a child to any other CommandNode");
        }

        if (_children.TryGetValue(node.GetName(), out var child))
        {
            if (node.GetCommand() != null)
            {
                child._command = node.GetCommand();
            }
            foreach (var grandchild in node.GetChildren())
            {
                child.AddChild(grandchild);
            }
        }
        else
        {
            _children[node.GetName()] = node;
            if (node is LiteralCommandNode<S> literal)
            {
                _literals[node.GetName()] = literal;
            }
            else if (node is ArgumentCommandNode<S> arg)
            {
                _arguments[node.GetName()] = arg;
            }
        }
    }

    public void FindAmbiguities(AmbiguityConsumer<S> consumer)
    {
        var matches = new HashSet<string>();

        foreach (var child in _children.Values)
        {
            foreach (var sibling in _children.Values)
            {
                if (ReferenceEquals(child, sibling)) continue;

                foreach (var input in child.GetExamples())
                {
                    if (sibling.IsValidInput(input))
                    {
                        matches.Add(input);
                    }
                }

                if (matches.Count > 0)
                {
                    consumer(this, child, sibling, matches);
                    matches = new HashSet<string>();
                }
            }

            child.FindAmbiguities(consumer);
        }
    }

    public override bool Equals(object? o)
    {
        if (ReferenceEquals(this, o)) return true;
        if (o is not CommandNode<S> that) return false;

        if (!ChildrenEquals(_children, that._children)) return false;
        if (!ReferenceEquals(_command, that._command)) return false;

        return true;
    }

    private static bool ChildrenEquals(Dictionary<string, CommandNode<S>> a, Dictionary<string, CommandNode<S>> b)
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
        return 31 * _children.GetHashCode() + (_command != null ? _command.GetHashCode() : 0);
    }

    public Predicate<S> GetRequirement() => _requirement;

    public abstract string GetName();
    public abstract string GetUsageText();
    public abstract void Parse(StringReader reader, CommandContextBuilder<S> contextBuilder);
    public abstract Task<Suggestions> ListSuggestions(CommandContext<S> context, SuggestionsBuilder builder);
    public abstract ArgumentBuilder<S> CreateBuilder();
    protected abstract string GetSortedKey();
    protected abstract bool IsValidInput(string input);

    //GetRelevantNodes 临时改cursor读literal文本后恢复优化定位
    public IReadOnlyCollection<CommandNode<S>> GetRelevantNodes(StringReader input)
    {
        if (_literals.Count > 0)
        {
            var cursor = input.Cursor;
            while (input.CanRead() && input.Peek() != ' ')
            {
                input.Skip();
            }
            var text = input.String.Substring(cursor, input.Cursor - cursor);
            input.SetCursor(cursor);
            if (_literals.TryGetValue(text, out var literal))
            {
                return new[] { (CommandNode<S>)literal };
            }
            return _arguments.Values.ToArray();
        }
        return _arguments.Values.ToArray();
    }

    public int CompareTo(CommandNode<S>? o)
    {
        if (o == null) return 1;
        var thisIsLiteral = this is LiteralCommandNode<S>;
        var oIsLiteral = o is LiteralCommandNode<S>;
        if (thisIsLiteral == oIsLiteral)
        {
            return string.Compare(GetSortedKey(), o.GetSortedKey(), StringComparison.Ordinal);
        }
        return oIsLiteral ? 1 : -1;
    }

    public bool IsFork() => _forks;

    public abstract IReadOnlyCollection<string> GetExamples();
}
