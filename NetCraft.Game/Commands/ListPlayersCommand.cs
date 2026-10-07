using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//ListPlayersCommand list command, maps to vanilla net.minecraft.server.commands.ListPlayersCommand
//Reports online players one by one; the uuids branch also includes the profile id
public static class ListPlayersCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("list")
            .Executes(context => ShowPlayers(context, false))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("uuids")
                .Executes(context => ShowPlayers(context, true))));
    }

    //ShowPlayers gives a summary line first then lists them; vanilla uses translatable, this assembles text directly
    private static int ShowPlayers(CommandContext<CommandSourceStack> context, bool withIds)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var players = source.Server.PlayerList.Players;
        source.SendSuccess($"there are {players.Count} players online, limit {source.Server.PlayerList.MaxPlayers}");
        foreach (var player in players)
            source.SendSuccess(withIds ? $"{player.Profile.Name} ({player.Profile.Id})" : player.Profile.Name);
        return players.Count;
    }
}
