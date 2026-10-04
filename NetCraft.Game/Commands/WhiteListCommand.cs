using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//WhiteListCommand whitelist 命令对应原版 net.minecraft.server.commands.WhitelistCommand
//开关白名单与增删成员 与原版一样目标只解析在线玩家名
public static class WhiteListCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("whitelist")
            .Requires(s => s.HasPermission(3))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("on")
                .Executes(context => SetEnabled(context, true)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("off")
                .Executes(context => SetEnabled(context, false)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("list")
                .Executes(List))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add")
                .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                    .Executes(context => Apply(context, true))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("remove")
                .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                    .Executes(context => Apply(context, false))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("reload")
                .Executes(Reload)));
    }

    //SetEnabled 开关白名单并写回 server.properties
    private static int SetEnabled(CommandContext<CommandSourceStack> context, bool enabled)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (source.Server.IsWhiteListEnabled == enabled)
        {
            source.SendFailure(enabled ? "白名单已处于开启状态" : "白名单已处于关闭状态");
            return 0;
        }

        source.Server.IsWhiteListEnabled = enabled;
        source.Server.Settings.SetWhiteList(enabled);
        source.Server.Settings.SaveCurrent();
        source.SendSuccess(enabled ? "已开启白名单" : "已关闭白名单");
        return 1;
    }

    //List 列出名单成员
    private static int List(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var names = source.Server.WhiteList.Names;
        source.SendSuccess($"白名单共 {names.Count} 名成员 当前{(source.Server.IsWhiteListEnabled ? "已开启" : "已关闭")}");
        foreach (var name in names) source.SendSuccess(name);
        return names.Count;
    }

    //Apply 增删名单成员 只认在线玩家 与原版支持离线档案不同
    private static int Apply(CommandContext<CommandSourceStack> context, bool add)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "target");
        var target = source.Server.PlayerList.GetPlayerByName(name);
        if (target is null)
        {
            source.SendFailure($"玩家 {name} 不在线");
            return 0;
        }

        if (add)
        {
            source.Server.WhiteList.Add(target.Profile);
            source.SendSuccess($"已将 {target.Profile.Name} 加入白名单");
            return 1;
        }

        if (!source.Server.WhiteList.Remove(target.Profile))
        {
            source.SendFailure($"{target.Profile.Name} 不在白名单内");
            return 0;
        }

        source.SendSuccess($"已将 {target.Profile.Name} 移出白名单");
        return 1;
    }

    //Reload 从磁盘重读名单
    private static int Reload(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        source.Server.WhiteList.Reload();
        source.SendSuccess($"已重新加载白名单 共 {source.Server.WhiteList.Count} 条");
        return 1;
    }
}
