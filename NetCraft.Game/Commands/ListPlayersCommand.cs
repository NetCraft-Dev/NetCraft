using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//ListPlayersCommand list 命令对应原版 net.minecraft.server.commands.ListPlayersCommand
//逐个回执在线玩家 uuids 分支额外带上档案 id
public static class ListPlayersCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("list")
            .Executes(context => ShowPlayers(context, false))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("uuids")
                .Executes(context => ShowPlayers(context, true))));
    }

    //ShowPlayers 先给一行汇总再逐个列出 原版走 translatable 这里直接拼文本
    private static int ShowPlayers(CommandContext<CommandSourceStack> context, bool withIds)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var players = source.Server.PlayerList.Players;
        source.SendSuccess($"当前有 {players.Count} 名玩家在线 上限 {source.Server.PlayerList.MaxPlayers}");
        foreach (var player in players)
            source.SendSuccess(withIds ? $"{player.Profile.Name} ({player.Profile.Id})" : player.Profile.Name);
        return players.Count;
    }
}
