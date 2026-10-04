using NetCraft.Game.Commands;
using NetCraft.Game.Server;

namespace NetCraft.Server.ServerConsole;

//ICompletionSource 补全来源
//实现返回的是"应用候选之后的完整输入文本" 控制台拿它整行替换 不必关心区间
public interface ICompletionSource
{
    //GetCompletions 算候选 text 是当前输入 caret 是光标位置
    IReadOnlyList<string> GetCompletions(string text, int caret);
}

//CommandCompletionSource 接 brigadier 命令树算补全
//与游戏里按 Tab 用的是同一个分派器 子命令与参数都能补出来
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
