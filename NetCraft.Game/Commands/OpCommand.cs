using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//OpCommand 管理员命令 op/deop 读写 ops.json 名单并即时同步在线目标权限等级
//原版目标参数是游戏档案可含离线玩家 本作只解析在线玩家名
public static class OpCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("op")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                .Executes(context => Apply(context, true))));
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("deop")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                .Executes(context => Apply(context, false))));
    }

    //Apply 按开关增删名单 在线目标立即刷新权限等级并同步客户端
    private static int Apply(CommandContext<CommandSourceStack> context, bool op)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "target");
        var target = source.Server.PlayerList.GetPlayerByName(name);
        if (target is null)
        {
            source.SendFailure($"玩家 {name} 不在线");
            return 0;
        }

        if (op)
        {
            var level = source.Server.Settings.OpPermissionLevel;
            source.Server.OpList.Add(target.Profile, level);
            source.Server.PlayerList.ApplyPermissionLevel(target, level);
            source.SendSuccess($"已将 {target.Profile.Name} 设为管理员 权限等级 {level}");
            return 1;
        }

        source.Server.OpList.Remove(target.Profile);
        source.Server.PlayerList.ApplyPermissionLevel(target, 0);
        source.SendSuccess($"已撤销 {target.Profile.Name} 的管理员权限");
        return 1;
    }
}
