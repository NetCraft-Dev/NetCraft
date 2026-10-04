using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//MsgCommand msg/tell/w 命令对应原版 net.minecraft.server.commands.MsgCommand
//私聊只发给目标与发送者本人 不广播给其他人
public static class MsgCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        //三个字面量共用同一棵子树 与原版注册 msg/tell/w 三个入口一致
        foreach (var name in new[] { "msg", "tell", "w" })
            dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal(name)
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("message", MessageArgument.Message())
                        .Executes(Send))));
    }

    //Send 逐目标投递并给发送者一条汇总回执
    private static int Send(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        if (targets.Count == 0)
        {
            source.SendFailure("没有找到匹配的玩家");
            return 0;
        }

        var senderName = source.SenderName;
        var message = MessageArgument.GetMessage(context, "message");
        var names = new List<string>(targets.Count);
        foreach (var target in targets)
        {
            names.Add(target.Profile.Name);
            target.SendSystemMessage(Component.Literal($"{senderName} 对你说: {message}"));
        }

        source.SendSuccess($"你对 {string.Join(", ", names)} 说: {message}");
        return targets.Count;
    }
}
