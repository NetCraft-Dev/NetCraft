using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Builder;

//LiteralArgumentBuilder literal builder, maps to vanilla com.mojang.brigadier.builder.LiteralArgumentBuilder
//Chains into a LiteralCommandNode; the static Literal method creates an instance
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
