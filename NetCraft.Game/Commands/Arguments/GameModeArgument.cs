using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands;
using NetCraft.Game.World.Level;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//GameModeArgument 游戏模式参数对应原版 net.minecraft.commands.arguments.GameModeArgument
//解析游戏模式全名或短名 与 /gamemode 参数类型(gameMode)网络id 对齐
public sealed class GameModeArgument : ArgumentType<GameType>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "survival", "creative" };

    //四个内置模式 供解析与补全遍历
    private static readonly GameType[] Values =
        { GameType.Survival, GameType.Creative, GameType.Adventure, GameType.Spectator };

    public static readonly DynamicCommandExceptionType ErrorInvalidGameMode =
        new(name => new LiteralMessage($"未知游戏模式 {name}"));

    public static GameModeArgument GameMode() => new();

    public GameType Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var name = reader.ReadString();
        var mode = GameType.ByName(name);
        if (mode is null)
        {
            reader.SetCursor(start);
            throw ErrorInvalidGameMode.CreateWithContext(reader, name);
        }
        return mode;
    }

    //GetGameMode 取解析出的游戏模式
    public static GameType GetGameMode(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<GameType>(name);

    //ListSuggestions 补全模式全名与短名 对应原版 GameModeArgument 的建议
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        foreach (var mode in Values)
        {
            if (mode.Name.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
                builder.Add(mode.Name);
            else if (mode.ShortName.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
                builder.Add(mode.ShortName);
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
