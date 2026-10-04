using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//SaveCommand save-all/save-off/save-on 命令对应原版 net.minecraft.server.commands.SaveAllCommand 等三个类
//save-all 立即全量刷盘 save-off/save-on 开关周期性自动刷盘
public static class SaveCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("save-all")
            .Requires(s => s.HasPermission(4))
            .Executes(All)
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("flush")
                .Executes(All)));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("save-off")
            .Requires(s => s.HasPermission(4))
            .Executes(context => SetAuto(context, false)));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("save-on")
            .Requires(s => s.HasPermission(4))
            .Executes(context => SetAuto(context, true)));
    }

    //All 立刻刷盘 全量写会阻塞主线程故先回执
    private static int All(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        source.SendSuccess("正在保存世界");
        source.Server.SaveAllNow();
        source.SendSuccess("世界已保存");
        return 1;
    }

    //SetAuto 开关自动刷盘 已经处于目标状态时提示不重复设置
    private static int SetAuto(CommandContext<CommandSourceStack> context, bool enabled)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (source.Server.IsSavingEnabled == enabled)
        {
            source.SendFailure(enabled ? "自动保存已处于开启状态" : "自动保存已处于关闭状态");
            return 0;
        }

        source.Server.SetSavingEnabled(enabled);
        source.SendSuccess(enabled ? "已开启自动保存" : "已关闭自动保存 记得用 save-all 手动刷盘");
        return 1;
    }
}
