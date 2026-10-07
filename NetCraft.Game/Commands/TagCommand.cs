using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//TagCommand tag command, maps to vanilla net.minecraft.server.commands.TagCommand
//tag <targets> add|remove <name> / list adds custom string tags to entities
//Tags live on the Registry.Entity base class; players are not Registry.Entity so player targets do not participate in tags
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

    //AddTag adds the tag to each target; reports failure when none were added, maps to vanilla addTag
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
            source.SendFailure($"no entity was tagged because the target already has the tag {name}");
            return 0;
        }
        source.SendSuccess(targets.Count == 1
            ? $"tagged {targets[0].Name} with {name}"
            : $"tagged {count} entities with {name}");
        return count;
    }

    //RemoveTag removes the tag from each target; reports failure when none were removed, maps to vanilla removeTag
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
            source.SendFailure($"no entity was untagged because the target does not have the tag {name}");
            return 0;
        }
        source.SendSuccess(targets.Count == 1
            ? $"removed tag {name} from {targets[0].Name}"
            : $"removed tag {name} from {count} entities");
        return count;
    }

    //ListTags aggregates all tags of the target set and reports, maps to vanilla listTags
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
                ? $"{targets[0].Name} has no tags"
                : $"{targets[0].Name} has {tags.Count} tags: {joined}");
        }
        else
        {
            source.SendSuccess(tags.Count == 0
                ? $"{targets.Count} targets have no tags"
                : $"{targets.Count} targets have {tags.Count} tags in total: {joined}");
        }
        return tags.Count;
    }

    //Entities takes the level entities from the target set; players have no tag storage and are skipped
    private static IEnumerable<Entity> Entities(IReadOnlyList<CommandTarget> targets)
    {
        foreach (var target in targets)
            if (target.WorldEntity is { } entity) yield return entity;
    }
}
