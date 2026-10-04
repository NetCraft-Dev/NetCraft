using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Server;
using NetCraft.Game.World.Level;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//GameRuleCommand /gamerule 命令对应原版 net.minecraft.server.commands.GameRuleCommand
//每条规则的短名与带命名空间形式各注册一份 不带值查询 带值修改
//布尔规则走 brigadier:bool 整数规则走 brigadier:integer 并按规则范围限制取值
public static class GameRuleCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var root = LiteralArgumentBuilder<CommandSourceStack>.Literal("gamerule")
            .Requires(s => s.HasPermission(2));
        foreach (var rule in GameRules.All)
        {
            root.Then(BuildRule(rule.Id.ToShortString(), rule));
            root.Then(BuildRule(rule.Id.ToString(), rule));
        }
        dispatcher.Register(root);
    }

    //BuildRule 单条规则的命令节点 查询与设置挂在同一个字面量下
    private static LiteralArgumentBuilder<CommandSourceStack> BuildRule(string literal, GameRule<object> rule)
    {
        var node = LiteralArgumentBuilder<CommandSourceStack>.Literal(literal)
            .Executes(context => QueryRule(context, rule));
        if (rule.Type == GameRuleType.Bool)
        {
            node.Then(RequiredArgumentBuilder<CommandSourceStack, bool>
                .Argument("value", BoolArgumentType.Bool())
                .Executes(context => SetRule(context, rule)));
        }
        else
        {
            node.Then(RequiredArgumentBuilder<CommandSourceStack, int>
                .Argument("value", IntegerArgumentType.Integer(rule.Min, rule.Max))
                .Executes(context => SetRule(context, rule)));
        }
        return node;
    }

    //QueryRule 查询规则当前值 对应原版 queryRule
    private static int QueryRule(CommandContext<CommandSourceStack> context, GameRule<object> rule)
    {
        var source = context.GetSource() as ServerCommandSource;
        if (source is null) return 0;
        var value = CurrentValue(source, rule);
        source.SendSuccess($"{rule.Id.ToShortString()} 当前为 {FormatValue(value)}");
        return CommandResult(rule, value);
    }

    //SetRule 修改规则值 对应原版 setRule
    private static int SetRule(CommandContext<CommandSourceStack> context, GameRule<object> rule)
    {
        var source = context.GetSource() as ServerCommandSource;
        if (source is null) return 0;
        object value = rule.Type == GameRuleType.Bool
            ? BoolArgumentType.GetBool(context, "value")
            : IntegerArgumentType.GetInteger(context, "value");
        source.Server.GameRules.SetRule(rule, value);
        source.SendSuccess($"已将 {rule.Id.ToShortString()} 设为 {FormatValue(value)}");
        return CommandResult(rule, value);
    }

    //CurrentValue 取规则当前值 没设置过时由存档回退到规则默认值
    private static object CurrentValue(ServerCommandSource source, GameRule<object> rule)
        => rule.Type == GameRuleType.Bool
            ? source.Server.GameRules.GetBool(rule)
            : source.Server.GameRules.GetInt(rule);

    //CommandResult 命令返回值 对应原版 getCommandResult 布尔规则返回 0/1 整数规则返回值本身
    private static int CommandResult(GameRule<object> rule, object value)
        => rule.Type == GameRuleType.Bool ? ((bool)value ? 1 : 0) : (int)value;

    //FormatValue 回执文案用原版的小写形式 布尔不能走 ToString 否则是 True/False
    private static string FormatValue(object value)
        => value is bool flag ? (flag ? "true" : "false") : value.ToString() ?? "";
}
