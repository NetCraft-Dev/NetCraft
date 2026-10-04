using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Level;

namespace NetCraft.Game.Commands;

//DefaultGameModeCommand defaultgamemode 命令对应原版 net.minecraft.server.commands.DefaultGameModeCommands
//改写默认游戏模式只影响之后加入的玩家 在线的要自己用 gamemode 切
public static class DefaultGameModeCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("defaultgamemode")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, GameType>.Argument("gamemode", GameModeArgument.GameMode())
                .Executes(Set)));
    }

    //Set 改运行时默认模式并写回 server.properties
    private static int Set(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var gameType = GameModeArgument.GetGameMode(context, "gamemode");
        source.Server.SetDefaultGameType(gameType);
        source.Server.Settings.SetGamemode(gameType.Name);
        source.Server.Settings.SaveCurrent();
        source.SendSuccess($"默认游戏模式已设为 {gameType.Name}");
        return 1;
    }
}
