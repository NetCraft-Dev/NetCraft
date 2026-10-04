using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//TellrawCommand tellraw 命令对应原版 net.minecraft.server.commands.TellRawCommand
//把组件按系统聊天发给目标玩家 与 title 一样不做组件内选择器解析
public static class TellrawCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("tellraw")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                .Then(RequiredArgumentBuilder<CommandSourceStack, Component>.Argument("message", ComponentArgument.TextComponent())
                    .Executes(context =>
                    {
                        var message = ComponentArgument.GetRawComponent(context, "message");
                        var players = EntityArgument.GetPlayers(context, "targets");
                        foreach (var player in players) player.SendSystemMessage(message);
                        return players.Count;
                    }))));
    }
}
