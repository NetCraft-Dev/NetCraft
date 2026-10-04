using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Commands.Tree;

namespace NetCraft.Game.Commands;

//HelpCommand help 命令对应原版 net.minecraft.server.commands.HelpCommand
//不带参数列出所有可用命令的简短用法 带命令名给出该命令的完整用法
//命令名走 brigadier:string 不用自定义参数类型: 命令树要整棵同步给客户端
//挂一个没注册进网络 id 表的参数类型会让整包在编码阶段被丢掉 客户端一条命令都看不到
public static class HelpCommand
{
    private const string Header = "--- 显示帮助 ---";

    private static readonly DynamicCommandExceptionType ErrorFailed =
        new(command => new TranslatableMessage("commands.help.failed", command));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("help")
            .Executes(c => ShowHelp(c, dispatcher))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("command", StringArgumentType.Word())
                .Suggests((context, builder) => SuggestCommandNames(context, dispatcher, builder))
                .Executes(c => ShowUsage(c, dispatcher, StringArgumentType.GetString(c, "command")))));
    }

    //ShowHelp 逐条列出根节点下每个可用命令的简短用法 对应原版 help 不带参数那支
    private static int ShowHelp(CommandContext<CommandSourceStack> context, CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var source = (ServerCommandSource)context.GetSource();
        var usages = dispatcher.GetSmartUsage(dispatcher.GetRoot(), source);
        source.SendSuccess(Header);
        foreach (var usage in usages.Values) source.SendSuccess("/" + usage);
        return usages.Count;
    }

    //ShowUsage 给出单个命令的完整用法 对应原版 help <command> 那支
    private static int ShowUsage(CommandContext<CommandSourceStack> context, CommandDispatcher<CommandSourceStack> dispatcher,
        string name)
    {
        var source = (ServerCommandSource)context.GetSource();
        //名字对不上直接报未知参数 与解析阶段失败一个效果
        var node = dispatcher.GetRoot().GetChild(name)
            ?? throw CommandSyntaxException.BuiltInExceptions.DispatcherUnknownArgument().Create();
        if (!node.CanUse(source)) throw ErrorFailed.Create(node.GetUsageText());
        source.SendSuccess(Header);
        var count = 0;
        //节点自身能执行时先报裸命令名 子路径再各报一条
        //用法文本是不含节点名的 这里得自己拼上 否则会显示成" /[<targets>]"这种没头没尾的样子
        if (node.GetCommand() is not null)
        {
            source.SendSuccess("/" + name);
            count++;
        }
        foreach (var usage in dispatcher.GetSmartUsage(node, source).Values)
        {
            source.SendSuccess("/" + name + " " + usage);
            count++;
        }
        //既没有执行体也没有子路径(例如只做重定向的 tp)时至少把命令名报出来
        if (count == 0) source.SendSuccess("/" + name);
        return count;
    }

    //SuggestCommandNames 给 help 的命令名补全 只列执行者有权用的
    private static Task<Suggestions> SuggestCommandNames(CommandContext<CommandSourceStack> context,
        CommandDispatcher<CommandSourceStack> dispatcher, SuggestionsBuilder builder)
    {
        var source = (ServerCommandSource)context.GetSource();
        var remaining = builder.Remaining;
        foreach (var child in dispatcher.GetRoot().GetChildren())
        {
            var name = child.GetUsageText();
            if (!name.StartsWith(remaining, StringComparison.Ordinal)) continue;
            if (!child.CanUse(source)) continue;
            builder.Add(name);
        }
        return builder.BuildFuture();
    }
}
