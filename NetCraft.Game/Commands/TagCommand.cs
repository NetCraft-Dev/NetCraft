using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//TagCommand tag 命令对应原版 net.minecraft.server.commands.TagCommand
//tag <targets> add|remove <name> / list 给实体打自定义字符串标签
//标签存在 Registry.Entity 基类上 玩家不是 Registry.Entity 玩家目标不参与标签
public static class TagCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("tag")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Entities())
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("name", StringArgumentType.Word())
                        .Executes(AddTag)))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("remove")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("name", StringArgumentType.Word())
                        .Executes(RemoveTag)))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("list")
                    .Executes(ListTags))));
    }

    //AddTag 给每个目标加标签 没有一个新增时报失败 对应原版 addTag
    private static int AddTag(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetEntities(context, "targets");
        var name = StringArgumentType.GetString(context, "name");
        var count = 0;
        foreach (var entity in Entities(targets))
            if (entity.AddTag(name)) count++;
        if (count == 0)
        {
            source.SendFailure($"没有实体被添加标签，因为目标已拥有标签 {name}");
            return 0;
        }
        source.SendSuccess(targets.Count == 1
            ? $"已给 {targets[0].Name} 添加标签 {name}"
            : $"已给 {count} 个实体添加标签 {name}");
        return count;
    }

    //RemoveTag 给每个目标摘标签 没有一个摘掉时报失败 对应原版 removeTag
    private static int RemoveTag(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetEntities(context, "targets");
        var name = StringArgumentType.GetString(context, "name");
        var count = 0;
        foreach (var entity in Entities(targets))
            if (entity.RemoveTag(name)) count++;
        if (count == 0)
        {
            source.SendFailure($"没有实体被移除标签，因为目标没有标签 {name}");
            return 0;
        }
        source.SendSuccess(targets.Count == 1
            ? $"已移除 {targets[0].Name} 的标签 {name}"
            : $"已移除 {count} 个实体的标签 {name}");
        return count;
    }

    //ListTags 汇总目标集合的全部标签并回执 对应原版 listTags
    private static int ListTags(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetEntities(context, "targets");
        var tags = new HashSet<string>();
        foreach (var entity in Entities(targets))
            tags.UnionWith(entity.GetTags());
        var joined = string.Join(", ", tags);
        if (targets.Count == 1)
        {
            source.SendSuccess(tags.Count == 0
                ? $"{targets[0].Name} 没有任何标签"
                : $"{targets[0].Name} 有 {tags.Count} 个标签: {joined}");
        }
        else
        {
            source.SendSuccess(tags.Count == 0
                ? $"{targets.Count} 个目标都没有标签"
                : $"{targets.Count} 个目标共有 {tags.Count} 个标签: {joined}");
        }
        return tags.Count;
    }

    //Entities 取目标集合里的关卡实体 玩家不具有标签存储 直接跳过
    private static IEnumerable<Entity> Entities(IReadOnlyList<CommandTarget> targets)
    {
        foreach (var target in targets)
            if (target.WorldEntity is { } entity) yield return entity;
    }
}
