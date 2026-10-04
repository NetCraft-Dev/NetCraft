using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;

namespace NetCraft.Commands.Tree;

//LiteralCommandNode 字面量节点对应原版com.mojang.brigadier.tree.LiteralCommandNode
//精确匹配字面量文本大小写敏感parse失败抛literalIncorrect
//listSuggestions按剩余文本前缀匹配预计算lowercase避免重复
public sealed class LiteralCommandNode<S> : CommandNode<S>
{
    private readonly string _literal;
    private readonly string _literalLowerCase;

    public LiteralCommandNode(string literal, Command<S>? command, Predicate<S> requirement, CommandNode<S>? redirect, RedirectModifier<S>? modifier, bool forks)
        : base(command, requirement, redirect, modifier, forks)
    {
        _literal = literal;
        _literalLowerCase = literal.ToLowerInvariant();
    }

    public string GetLiteral() => _literal;

    public override string GetName() => _literal;

    public override void Parse(StringReader reader, CommandContextBuilder<S> contextBuilder)
    {
        var start = reader.Cursor;
        var end = ParseLiteral(reader);
        if (end > -1)
        {
            contextBuilder.WithNode(this, StringRange.Between(start, end));
            return;
        }
        throw CommandSyntaxException.BuiltInExceptions.LiteralIncorrect().CreateWithContext(reader, _literal);
    }

    //ParseLiteral 私有解析literal文本返回结束cursor或-1不匹配回退cursor
    private int ParseLiteral(StringReader reader)
    {
        var start = reader.Cursor;
        if (reader.CanRead(_literal.Length))
        {
            var end = start + _literal.Length;
            if (reader.String.Substring(start, _literal.Length).Equals(_literal))
            {
                reader.SetCursor(end);
                if (!reader.CanRead() || reader.Peek() == ' ')
                {
                    return end;
                }
                reader.SetCursor(start);
            }
        }
        return -1;
    }

    public override Task<Suggestions> ListSuggestions(CommandContext<S> context, SuggestionsBuilder builder)
    {
        if (_literalLowerCase.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
        {
            builder.Add(_literal);
            return builder.BuildFuture();
        }
        return Suggestions.Empty();
    }

    protected override bool IsValidInput(string input)
    {
        return ParseLiteral(new StringReader(input)) > -1;
    }

    public override bool Equals(object? o)
    {
        if (ReferenceEquals(this, o)) return true;
        if (o is not LiteralCommandNode<S> that) return false;
        if (!_literal.Equals(that._literal)) return false;
        return base.Equals(o);
    }

    public override string GetUsageText() => _literal;

    public override int GetHashCode()
    {
        var result = _literal.GetHashCode();
        result = 31 * result + base.GetHashCode();
        return result;
    }

    public override LiteralArgumentBuilder<S> CreateBuilder()
    {
        var builder = LiteralArgumentBuilder<S>.Literal(_literal);
        builder.Requires(GetRequirement());
        builder.Forward(GetRedirect(), GetRedirectModifier(), IsFork());
        if (GetCommand() != null)
        {
            builder.Executes(GetCommand()!);
        }
        return builder;
    }

    protected override string GetSortedKey() => _literal;

    public override IReadOnlyCollection<string> GetExamples() => new[] { _literal };

    public override string ToString() => $"<literal {_literal}>";
}
