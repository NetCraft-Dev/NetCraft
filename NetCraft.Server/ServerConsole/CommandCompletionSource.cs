using NetCraft.Game.Commands;
using NetCraft.Game.Server;

namespace NetCraft.Server.ServerConsole;

//ICompletionSource, completion source
//Implementations return the "full input text after applying the candidate", the console replaces the whole line with it and need not track ranges
public interface ICompletionSource
{
    //GetCompletions computes candidates, text is the current input and caret is the cursor position
    IReadOnlyList<string> GetCompletions(string text, int caret);
}

//CommandCompletionSource computes completions from the brigadier command tree
//Uses the same dispatcher as pressing Tab in-game, both subcommands and arguments can be completed
public sealed class CommandCompletionSource : ICompletionSource
{
    private readonly DedicatedServer _server;

    public CommandCompletionSource(DedicatedServer server) => _server = server;

    public IReadOnlyList<string> GetCompletions(string text, int caret)
    {
        var source = ServerCommandSource.Console(_server);
        var suggestions = _server.Commands.GetCompletions(source, text, caret);
        if (suggestions.IsEmpty()) return Array.Empty<string>();

        var results = new List<string>(suggestions.List.Count);
        foreach (var suggestion in suggestions.List)
            results.Add(suggestion.Apply(text));
        return results;
    }
}
