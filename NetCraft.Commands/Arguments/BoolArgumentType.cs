using NetCraft.Commands.Context;
using NetCraft.Commands.Suggestion;

namespace NetCraft.Commands.Arguments;

//BoolArgumentType boolean argument type, maps to vanilla com.mojang.brigadier.arguments.BoolArgumentType
//Parses true or false and provides completions
public sealed class BoolArgumentType : ArgumentType<bool>
{
    private static readonly IReadOnlyList<string> _examples = new[] { "true", "false" };

    private BoolArgumentType()
    {
    }

    public static BoolArgumentType Bool() => new();

    public static bool GetBool<S>(CommandContext<S> context, string name)
    {
        return context.GetArgument<bool>(name);
    }

    public bool Parse(StringReader reader)
    {
        return reader.ReadBoolean();
    }

    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        if ("true".StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
        {
            builder.Add("true");
        }
        if ("false".StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
        {
            builder.Add("false");
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => _examples;

    public override bool Equals(object? obj) => obj is BoolArgumentType;

    public override int GetHashCode() => 0;

    public override string ToString() => "bool()";
}
