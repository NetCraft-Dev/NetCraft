using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Tree;

namespace NetCraft.Commands;

//ParseResults 解析结果对应原版com.mojang.brigadier.ParseResults
//持CommandContextBuilder与未消费reader与各子节点解析异常表
//解析永不失败调用方根据reader.CanRead与exceptions判断有效性
public sealed class ParseResults<S>
{
    private readonly CommandContextBuilder<S> _context;
    private readonly IReadOnlyDictionary<CommandNode<S>, CommandSyntaxException> _exceptions;
    private readonly IImmutableStringReader _reader;

    public ParseResults(CommandContextBuilder<S> context, IImmutableStringReader reader, IReadOnlyDictionary<CommandNode<S>, CommandSyntaxException> exceptions)
    {
        _context = context;
        _reader = reader;
        _exceptions = exceptions;
    }

    public ParseResults(CommandContextBuilder<S> context)
        : this(context, new StringReader(""), new Dictionary<CommandNode<S>, CommandSyntaxException>())
    {
    }

    public CommandContextBuilder<S> GetContext() => _context;

    public IImmutableStringReader GetReader() => _reader;

    public IReadOnlyDictionary<CommandNode<S>, CommandSyntaxException> GetExceptions() => _exceptions;
}
