using System.Globalization;
using System.Text;

using NetCraft.Codec;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Commands.Tree;

namespace NetCraft.Commands;

//CommandDispatcher 命令分发器对应原版com.mojang.brigadier.CommandDispatcher
//持RootCommandNode命令树与ResultConsumer结果回调提供注册解析执行补全
public sealed class CommandDispatcher<S>
{
    public const string ARGUMENT_SEPARATOR = " ";
    public const char ARGUMENT_SEPARATOR_CHAR = ' ';

    private const string USAGE_OPTIONAL_OPEN = "[";
    private const string USAGE_OPTIONAL_CLOSE = "]";
    private const string USAGE_REQUIRED_OPEN = "(";
    private const string USAGE_REQUIRED_CLOSE = ")";
    private const string USAGE_OR = "|";

    private readonly RootCommandNode<S> _root;
    private ResultConsumer<S> _consumer = (_, _, _) => { };

    public CommandDispatcher(RootCommandNode<S> root)
    {
        _root = root;
    }

    public CommandDispatcher() : this(new RootCommandNode<S>())
    {
    }

    //HasCommand 递归检查子树是否存在command非null的节点供getSmartUsage过滤
    private bool HasCommand(CommandNode<S> input)
    {
        if (input == null) return false;
        if (input.GetCommand() != null) return true;
        foreach (var child in input.GetChildren())
        {
            if (HasCommand(child)) return true;
        }
        return false;
    }

    //Register 注册字面量命令到根节点返回构建结果
    public LiteralCommandNode<S> Register(LiteralArgumentBuilder<S> command)
    {
        var build = command.Build();
        _root.AddChild(build);
        return build;
    }

    public void SetConsumer(ResultConsumer<S> consumer)
    {
        _consumer = consumer;
    }

    public int Execute(string input, S source)
    {
        return Execute(new StringReader(input), source);
    }

    public int Execute(StringReader input, S source)
    {
        var parse = Parse(input, source);
        return Execute(parse);
    }

    //Execute 检查reader未消费部分根据exceptions或context.range抛异常再用ContextChain展平执行
    public int Execute(ParseResults<S> parse)
    {
        if (parse.GetReader().CanRead())
        {
            if (parse.GetExceptions().Count == 1)
            {
                throw parse.GetExceptions().Values.First();
            }
            if (parse.GetContext().GetRange().IsEmpty())
            {
                throw CommandSyntaxException.BuiltInExceptions.DispatcherUnknownCommand().CreateWithContext(parse.GetReader());
            }
            throw CommandSyntaxException.BuiltInExceptions.DispatcherUnknownArgument().CreateWithContext(parse.GetReader());
        }

        var command = parse.GetReader().String;
        var original = parse.GetContext().Build(command);

        var flatContext = ContextChain<S>.TryFlatten(original);
        if (!flatContext.IsPresent)
        {
            _consumer(original, false, 0);
            throw CommandSyntaxException.BuiltInExceptions.DispatcherUnknownCommand().CreateWithContext(parse.GetReader());
        }

        return flatContext.Get().ExecuteAll(original.GetSource(), _consumer);
    }

    public ParseResults<S> Parse(string command, S source)
    {
        return Parse(new StringReader(command), source);
    }

    public ParseResults<S> Parse(StringReader command, S source)
    {
        var context = new CommandContextBuilder<S>(this, source, _root, command.Cursor);
        return ParseNodes(_root, command, context);
    }

    //ParseNodes 递归解析命令树各子节点收集potential与error按canRead/exceptions排序选最优
    //redirect路径递归parseNodes到目标节点其他路径继续递归到子节点
    private ParseResults<S> ParseNodes(CommandNode<S> node, StringReader originalReader, CommandContextBuilder<S> contextSoFar)
    {
        var source = contextSoFar.GetSource();
        Dictionary<CommandNode<S>, CommandSyntaxException>? errors = null;
        List<ParseResults<S>>? potentials = null;
        var cursor = originalReader.Cursor;

        foreach (var child in node.GetRelevantNodes(originalReader))
        {
            if (!child.CanUse(source))
            {
                continue;
            }
            var context = contextSoFar.Copy();
            var reader = new StringReader(originalReader);
            try
            {
                try
                {
                    child.Parse(reader, context);
                }
                catch (Exception ex) when (ex is not CommandSyntaxException)
                {
                    throw CommandSyntaxException.BuiltInExceptions.DispatcherParseException().CreateWithContext(reader, ex.Message);
                }
                if (reader.CanRead())
                {
                    if (reader.Peek() != ARGUMENT_SEPARATOR_CHAR)
                    {
                        throw CommandSyntaxException.BuiltInExceptions.DispatcherExpectedArgumentSeparator().CreateWithContext(reader);
                    }
                }
            }
            catch (CommandSyntaxException ex)
            {
                errors ??= new Dictionary<CommandNode<S>, CommandSyntaxException>();
                errors[child] = ex;
                reader.SetCursor(cursor);
                continue;
            }

            context.WithCommand(child.GetCommand());
            if (reader.CanRead(child.GetRedirect() == null ? 2 : 1))
            {
                reader.Skip();
                var redirect = child.GetRedirect();
                if (redirect != null)
                {
                    var childContext = new CommandContextBuilder<S>(this, source, redirect, reader.Cursor);
                    var redirectParse = ParseNodes(redirect, reader, childContext);
                    context.WithChild(redirectParse.GetContext());
                    return new ParseResults<S>(context, redirectParse.GetReader(), redirectParse.GetExceptions());
                }
                var subParse = ParseNodes(child, reader, context);
                potentials ??= new List<ParseResults<S>>(1);
                potentials.Add(subParse);
            }
            else
            {
                potentials ??= new List<ParseResults<S>>(1);
                potentials.Add(new ParseResults<S>(context, reader, new Dictionary<CommandNode<S>, CommandSyntaxException>()));
            }
        }

        if (potentials != null)
        {
            if (potentials.Count > 1)
            {
                potentials.Sort((a, b) =>
                {
                    if (!a.GetReader().CanRead() && b.GetReader().CanRead()) return -1;
                    if (a.GetReader().CanRead() && !b.GetReader().CanRead()) return 1;
                    if (a.GetExceptions().Count == 0 && b.GetExceptions().Count > 0) return -1;
                    if (a.GetExceptions().Count > 0 && b.GetExceptions().Count == 0) return 1;
                    return 0;
                });
            }
            return potentials[0];
        }

        return new ParseResults<S>(contextSoFar, originalReader, errors ?? new Dictionary<CommandNode<S>, CommandSyntaxException>());
    }

    public string[] GetAllUsage(CommandNode<S> node, S source, bool restricted)
    {
        var result = new List<string>();
        GetAllUsage(node, source, result, "", restricted);
        return result.ToArray();
    }

    private void GetAllUsage(CommandNode<S> node, S source, List<string> result, string prefix, bool restricted)
    {
        if (restricted && !node.CanUse(source))
        {
            return;
        }

        if (node.GetCommand() != null)
        {
            result.Add(prefix);
        }

        if (node.GetRedirect() != null)
        {
            var redirect = ReferenceEquals(node.GetRedirect(), _root) ? "..." : "-> " + node.GetRedirect()!.GetUsageText();
            result.Add(prefix.Length == 0 ? node.GetUsageText() + ARGUMENT_SEPARATOR + redirect : prefix + ARGUMENT_SEPARATOR + redirect);
        }
        else if (node.GetChildren().Count > 0)
        {
            foreach (var child in node.GetChildren())
            {
                GetAllUsage(child, source, result, prefix.Length == 0 ? child.GetUsageText() : prefix + ARGUMENT_SEPARATOR + child.GetUsageText(), restricted);
            }
        }
    }

    public IReadOnlyDictionary<CommandNode<S>, string> GetSmartUsage(CommandNode<S> node, S source)
    {
        var result = new Dictionary<CommandNode<S>, string>();

        var optional = node.GetCommand() != null;
        foreach (var child in node.GetChildren())
        {
            var usage = GetSmartUsage(child, source, optional, false);
            if (usage != null)
            {
                result[child] = usage;
            }
        }
        return result;
    }

    private string? GetSmartUsage(CommandNode<S> node, S source, bool optional, bool deep)
    {
        if (!node.CanUse(source))
        {
            return null;
        }

        var self = optional ? USAGE_OPTIONAL_OPEN + node.GetUsageText() + USAGE_OPTIONAL_CLOSE : node.GetUsageText();
        var childOptional = node.GetCommand() != null;
        var open = childOptional ? USAGE_OPTIONAL_OPEN : USAGE_REQUIRED_OPEN;
        var close = childOptional ? USAGE_OPTIONAL_CLOSE : USAGE_REQUIRED_CLOSE;

        if (!deep)
        {
            if (node.GetRedirect() != null)
            {
                var redirect = ReferenceEquals(node.GetRedirect(), _root) ? "..." : "-> " + node.GetRedirect()!.GetUsageText();
                return self + ARGUMENT_SEPARATOR + redirect;
            }
            var children = node.GetChildren().Where(c => c.CanUse(source)).ToList();
            if (children.Count == 1)
            {
                var usage = GetSmartUsage(children[0], source, childOptional, childOptional);
                if (usage != null)
                {
                    return self + ARGUMENT_SEPARATOR + usage;
                }
            }
            else if (children.Count > 1)
            {
                var childUsage = new HashSet<string>();
                foreach (var child in children)
                {
                    var usage = GetSmartUsage(child, source, childOptional, true);
                    if (usage != null)
                    {
                        childUsage.Add(usage);
                    }
                }
                if (childUsage.Count == 1)
                {
                    var usage = childUsage.First();
                    return self + ARGUMENT_SEPARATOR + (childOptional ? USAGE_OPTIONAL_OPEN + usage + USAGE_OPTIONAL_CLOSE : usage);
                }
                if (childUsage.Count > 1)
                {
                    var builder = new StringBuilder(open);
                    var count = 0;
                    foreach (var child in children)
                    {
                        if (count > 0)
                        {
                            builder.Append(USAGE_OR);
                        }
                        builder.Append(child.GetUsageText());
                        count++;
                    }
                    if (count > 0)
                    {
                        builder.Append(close);
                        return self + ARGUMENT_SEPARATOR + builder;
                    }
                }
            }
        }

        return self;
    }

    //GetCompletionSuggestions 异步收集cursor位置节点所有子节点的建议并合并返回
    public Task<Suggestions> GetCompletionSuggestions(ParseResults<S> parse)
    {
        return GetCompletionSuggestions(parse, parse.GetReader().TotalLength);
    }

    public async Task<Suggestions> GetCompletionSuggestions(ParseResults<S> parse, int cursor)
    {
        var context = parse.GetContext();

        var nodeBeforeCursor = context.FindSuggestionContext(cursor);
        var parent = nodeBeforeCursor.Node;
        var start = Math.Min(nodeBeforeCursor.StartPos, cursor);

        var fullInput = parse.GetReader().String;
        var truncatedInput = fullInput[..cursor];
        var truncatedInputLowerCase = truncatedInput.ToLower(CultureInfo.InvariantCulture);

        var futures = new List<Task<Suggestions>>();
        foreach (var node in parent.GetChildren())
        {
            var future = Suggestions.Empty();
            try
            {
                future = node.ListSuggestions(nodeBeforeCursor.Parent.Build(truncatedInput), new SuggestionsBuilder(truncatedInput, truncatedInputLowerCase, start));
            }
            catch (CommandSyntaxException)
            {
            }
            futures.Add(future);
        }

        var results = await Task.WhenAll(futures);
        return await Suggestions.Merge(fullInput, results.ToList());
    }

    public RootCommandNode<S> GetRoot() => _root;

    //GetPath 深度优先遍历查找目标节点收集路径节点名
    public IReadOnlyList<string> GetPath(CommandNode<S> target)
    {
        var nodes = new List<List<CommandNode<S>>>();
        AddPaths(_root, nodes, new List<CommandNode<S>>());

        foreach (var list in nodes)
        {
            if (ReferenceEquals(list[list.Count - 1], target))
            {
                var result = new List<string>(list.Count);
                foreach (var node in list)
                {
                    if (!ReferenceEquals(node, _root))
                    {
                        result.Add(node.GetName());
                    }
                }
                return result;
            }
        }

        return Array.Empty<string>();
    }

    public CommandNode<S>? FindNode(IEnumerable<string> path)
    {
        CommandNode<S>? node = _root;
        foreach (var name in path)
        {
            node = node.GetChild(name);
            if (node == null)
            {
                return null;
            }
        }
        return node;
    }

    public void FindAmbiguities(AmbiguityConsumer<S> consumer)
    {
        _root.FindAmbiguities(consumer);
    }

    private void AddPaths(CommandNode<S> node, List<List<CommandNode<S>>> result, List<CommandNode<S>> parents)
    {
        var current = new List<CommandNode<S>>(parents);
        current.Add(node);
        result.Add(current);

        foreach (var child in node.GetChildren())
        {
            AddPaths(child, result, current);
        }
    }
}
