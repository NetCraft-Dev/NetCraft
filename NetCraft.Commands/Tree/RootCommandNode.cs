using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Suggestion;

namespace NetCraft.Commands.Tree;

//RootCommandNode 根节点对应原版com.mojang.brigadier.tree.RootCommandNode
//dispatcher树顶层节点无命令无重定向modifier返回单源集合
public sealed class RootCommandNode<S> : CommandNode<S>
{
    public RootCommandNode()
        : base(null, _ => true, null, ctx => new[] { ctx.GetSource() }, false)
    {
    }

    public override string GetName() => "";

    public override string GetUsageText() => "";

    public override void Parse(StringReader reader, CommandContextBuilder<S> contextBuilder)
    {
    }

    public override Task<Suggestions> ListSuggestions(CommandContext<S> context, SuggestionsBuilder builder)
    {
        return Suggestions.Empty();
    }

    protected override bool IsValidInput(string input) => false;

    public override bool Equals(object? o)
    {
        if (ReferenceEquals(this, o)) return true;
        if (o is not RootCommandNode<S>) return false;
        return base.Equals(o);
    }

    public override int GetHashCode() => base.GetHashCode();

    public override ArgumentBuilder<S> CreateBuilder()
    {
        throw new InvalidOperationException("Cannot convert root into a builder");
    }

    protected override string GetSortedKey() => "";

    public override IReadOnlyCollection<string> GetExamples() => Array.Empty<string>();

    public override string ToString() => "<root>";
}
