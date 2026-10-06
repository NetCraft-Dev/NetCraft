using NetCraft.Commands.Context;
using NetCraft.Commands.Suggestion;

namespace NetCraft.Commands.Arguments;

//ArgumentType interface maps to vanilla com.mojang.brigadier.arguments.ArgumentType
//Every argument parser implements Parse to read a value from a StringReader, and ListSuggestions to provide completions
public interface ArgumentType<T>
{
    T Parse(StringReader reader);

    //Parse overload with source, forwards to the source-less version by default
    T Parse<S>(StringReader reader, S source) => Parse(reader);

    //ListSuggestions returns empty suggestions by default
    Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
        => Suggestions.Empty();

    IReadOnlyList<string> Examples => Array.Empty<string>();
}
