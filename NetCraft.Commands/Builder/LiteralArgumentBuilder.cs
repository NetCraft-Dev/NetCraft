using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Builder;

//LiteralArgumentBuilder 字面量构建器对应原版com.mojang.brigadier.builder.LiteralArgumentBuilder
//链式构建LiteralCommandNode静态Literal方法创建实例
public sealed class LiteralArgumentBuilder<S> : ArgumentBuilder<S, LiteralArgumentBuilder<S>>
{
    private readonly string _literal;

    private LiteralArgumentBuilder(string literal)
    {
        _literal = literal;
    }

    public static LiteralArgumentBuilder<S> Literal(string name) => new(name);

    protected override LiteralArgumentBuilder<S> GetThis() => this;

    public string GetLiteral() => _literal;

    public override LiteralCommandNode<S> Build()
    {
        var result = new LiteralCommandNode<S>(_literal, GetCommand(), GetRequirement(), GetRedirect(), GetRedirectModifier(), IsFork());

        foreach (var argument in GetArguments())
        {
            result.AddChild(argument);
        }

        return result;
    }
}
