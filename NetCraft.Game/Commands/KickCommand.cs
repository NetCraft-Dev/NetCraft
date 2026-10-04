using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Server;
using NetCraft.Logging;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//KickCommand 踢出命令 对应原版 net.minecraft.server.commands.KickCommand
//目标只解析在线玩家名 本作没有离线玩家档案解析(与 /op 同一限制)
//理由缺省时用原版 multiplayer.disconnect.kicked 客户端按本地语言显示
public static class KickCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("kick")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("targets", StringArgumentType.Word())
                .Executes(context => Kick(context, null))
                .Then(RequiredArgumentBuilder<CommandSourceStack, string>
                    .Argument("reason", StringArgumentType.GreedyString())
                    .Executes(context => Kick(context, StringArgumentType.GetString(context, "reason"))))));
    }

    //Kick 断开目标连接 对应原版 kickPlayers
    private static int Kick(CommandContext<CommandSourceStack> context, string? reason)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "targets");
        var target = source.Server.PlayerList.GetPlayerByName(name);
        if (target is null)
        {
            source.SendFailure($"玩家 {name} 不在线");
            return 0;
        }
        target.Disconnect(reason is null
            ? Component.Translatable("multiplayer.disconnect.kicked")
            : Component.Literal(reason));
        //操作者可能是控制台 这里不能走 PlayerOrThrow 它抛出来会把后面的成功回执一起带走
        Log.Info($"Kicked player {target.Profile.Name} operator={source.Player?.Profile.Name ?? "Server"} reason={reason ?? "none"}");
        source.SendSuccess(reason is null
            ? $"已将 {target.Profile.Name} 踢出服务器"
            : $"已将 {target.Profile.Name} 踢出服务器 理由: {reason}");
        return 1;
    }
}
