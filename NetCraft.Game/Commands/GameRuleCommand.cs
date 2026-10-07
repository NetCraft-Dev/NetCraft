using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Server;
using NetCraft.Game.World.Level;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//GameRuleCommand /gamerule command, maps to vanilla net.minecraft.server.commands.GameRuleCommand
//Each rule is registered both by short name and by namespaced form; without a value it queries, with a value it sets
//Boolean rules use brigadier:bool and integer rules use brigadier:integer, bound by the rule's range
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

    //BuildRule the command node for a single rule; query and set hang under the same literal
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

    //QueryRule queries the rule's current value, maps to vanilla queryRule
    private static int QueryRule(CommandContext<CommandSourceStack> context, GameRule<object> rule)
    {
        var source = context.GetSource() as ServerCommandSource;
        if (source is null) return 0;
        var value = CurrentValue(source, rule);
        source.SendSuccess($"{rule.Id.ToShortString()} is currently {FormatValue(value)}");
        return CommandResult(rule, value);
    }

    //SetRule changes the rule value, maps to vanilla setRule
    private static int SetRule(CommandContext<CommandSourceStack> context, GameRule<object> rule)
    {
        var source = context.GetSource() as ServerCommandSource;
        if (source is null) return 0;
        object value = rule.Type == GameRuleType.Bool
            ? BoolArgumentType.GetBool(context, "value")
            : IntegerArgumentType.GetInteger(context, "value");
        source.Server.GameRules.SetRule(rule, value);
        source.SendSuccess($"set {rule.Id.ToShortString()} to {FormatValue(value)}");
        return CommandResult(rule, value);
    }

    //CurrentValue gets the rule's current value; when never set it falls back to the rule default from the save
    private static object CurrentValue(ServerCommandSource source, GameRule<object> rule)
        => rule.Type == GameRuleType.Bool
            ? source.Server.GameRules.GetBool(rule)
            : source.Server.GameRules.GetInt(rule);

    //CommandResult the command return value, maps to vanilla getCommandResult; boolean rules return 0/1, integer rules return the value itself
    private static int CommandResult(GameRule<object> rule, object value)
        => rule.Type == GameRuleType.Bool ? ((bool)value ? 1 : 0) : (int)value;

    //FormatValue uses the vanilla lowercase form for the reply text; booleans must not go through ToString or it is True/False
    private static string FormatValue(object value)
        => value is bool flag ? (flag ? "true" : "false") : value.ToString() ?? "";
}
