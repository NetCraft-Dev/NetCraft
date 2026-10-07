using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Level;

namespace NetCraft.Game.Commands;

//DefaultGameModeCommand defaultgamemode command, maps to vanilla net.minecraft.server.commands.DefaultGameModeCommands
//Changing the default game mode only affects players joining later; online ones must switch with gamemode themselves
public static class DefaultGameModeCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("defaultgamemode")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, GameType>.Argument("gamemode", GameModeArgument.GameMode())
                .Executes(Set)));
    }

    //Set changes the runtime default mode and writes it back to server.properties
    private static int Set(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var gameType = GameModeArgument.GetGameMode(context, "gamemode");
        source.Server.SetDefaultGameType(gameType);
        source.Server.Settings.SetGamemode(gameType.Name);
        source.Server.Settings.SaveCurrent();
        source.SendSuccess($"the default game mode is now {gameType.Name}");
        return 1;
    }
}
