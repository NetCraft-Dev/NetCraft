using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//StopwatchCommand stopwatch 命令对应原版 net.minecraft.server.commands.StopwatchCommand
//按 id 创建查询重启移除调试计时器 查询的返回值是耗时秒数乘倍率
public static class StopwatchCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("stopwatch")
            .Requires(s => s.HasPermission(2))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("create")
                .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                    .Executes(Create)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("query")
                .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                    .Executes(context => Query(context, 1.0))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("scale", DoubleArgumentType.DoubleArg())
                        .Executes(context => Query(context, DoubleArgumentType.GetDouble(context, "scale"))))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("restart")
                .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                    .Executes(Restart)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("remove")
                .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                    .Executes(Remove))));
    }

    //Create 新建计时器 重名则报错
    private static int Create(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var id = IdentifierArgument.GetId(context, "id");
        if (!source.Server.Stopwatches.Add(id, Stopwatches.CurrentTime()))
        {
            source.SendFailure($"计时器 {id} 已存在");
            return 0;
        }

        source.SendSuccess($"已创建计时器 {id}");
        return 1;
    }

    //Query 回执经过的秒数 返回值按倍率缩放
    private static int Query(CommandContext<CommandSourceStack> context, double scale)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var id = IdentifierArgument.GetId(context, "id");
        if (source.Server.Stopwatches.GetStart(id) is not long start)
        {
            source.SendFailure($"计时器 {id} 不存在");
            return 0;
        }

        var elapsed = (Stopwatches.CurrentTime() - start) / 1000.0;
        source.SendSuccess($"计时器 {id} 已运行 {elapsed:F3} 秒");
        return (int)(elapsed * scale);
    }

    //Restart 把计时器重置到现在
    private static int Restart(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var id = IdentifierArgument.GetId(context, "id");
        if (!source.Server.Stopwatches.Restart(id, Stopwatches.CurrentTime()))
        {
            source.SendFailure($"计时器 {id} 不存在");
            return 0;
        }

        source.SendSuccess($"已重启计时器 {id}");
        return 1;
    }

    //Remove 移除计时器
    private static int Remove(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var id = IdentifierArgument.GetId(context, "id");
        if (!source.Server.Stopwatches.Remove(id))
        {
            source.SendFailure($"计时器 {id} 不存在");
            return 0;
        }

        source.SendSuccess($"已移除计时器 {id}");
        return 1;
    }
}
