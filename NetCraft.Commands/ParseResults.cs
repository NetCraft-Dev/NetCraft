using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Tree;

namespace NetCraft.Commands;

//ParseResults maps to vanilla com.mojang.brigadier.ParseResults
//Holds a CommandContextBuilder, the unconsumed reader and the parse exception table of each child node
//Parsing never fails; callers judge validity from reader.CanRead and the exceptions
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
