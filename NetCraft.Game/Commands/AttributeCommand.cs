using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Registry;
//Attribute-related types carry their own namespace; only the few names needed are taken here
using AttributeInstance = NetCraft.Registry.EntityAttribute.AttributeInstance;
using AttributeMap = NetCraft.Registry.EntityAttribute.AttributeMap;
using AttributeDef = NetCraft.Registry.EntityAttribute.Attribute;
using AttributeModifier = NetCraft.Registry.EntityAttribute.AttributeModifier;
using AttributeOperation = NetCraft.Registry.EntityAttribute.AttributeOperation;

namespace NetCraft.Game.Commands;

//AttributeCommand attribute command, maps to vanilla net.minecraft.server.commands.AttributeCommand
//attribute <target> <attribute> followed by the three branches get / base / modifier
//base includes set/get/reset; modifier includes add/remove/value; get covers everything, aligned with vanilla
public static class AttributeCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("attribute")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("target", EntityArgument.Entity())
                .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("attribute",
                        new ResourceArgument(Registries.ATTRIBUTE.Identifier))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("get")
                        .Executes(context => GetValue(context, 1.0))
                        .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("scale", DoubleArgumentType.DoubleArg())
                            .Executes(context => GetValue(context, DoubleArgumentType.GetDouble(context, "scale")))))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("base")
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("set")
                            .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("value", DoubleArgumentType.DoubleArg())
                                .Executes(SetBase)))
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("get")
                            .Executes(context => GetBase(context, 1.0))
                            .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("scale", DoubleArgumentType.DoubleArg())
                                .Executes(context => GetBase(context, DoubleArgumentType.GetDouble(context, "scale")))))
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("reset")
                            .Executes(ResetBase)))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("modifier")
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add")
                            .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                                .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("value", DoubleArgumentType.DoubleArg())
                                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add_value")
                                        .Executes(context => AddModifier(context, AttributeOperation.AddValue)))
                                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add_multiplied_base")
                                        .Executes(context => AddModifier(context, AttributeOperation.AddMultipliedBase)))
                                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add_multiplied_total")
                                        .Executes(context => AddModifier(context, AttributeOperation.AddMultipliedTotal))))))
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("remove")
                            .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                                .Executes(RemoveModifier)))
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("value")
                            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("get")
                                .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                                    .Executes(context => GetModifierValue(context, 1.0))
                                    .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("scale", DoubleArgumentType.DoubleArg())
                                        .Executes(context => GetModifierValue(context, DoubleArgumentType.GetDouble(context, "scale")))))))))));
    }

    //GetValue reads back the final attribute value, maps to vanilla getAttributeValue
    private static int GetValue(CommandContext<CommandSourceStack> context, double scale)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"unknown attribute {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        var map = MapOf(target);
        if (!map.HasAttribute(attribute))
        {
            source.SendFailure($"{target.Name} has no attribute {Describe(attribute)}");
            return 0;
        }
        var value = map.GetValue(attribute);
        source.SendSuccess($"{Describe(attribute)} on {target.Name} is {value}");
        return (int)(value * scale);
    }

    //GetBase reads back the attribute base value, maps to vanilla getAttributeBase
    private static int GetBase(CommandContext<CommandSourceStack> context, double scale)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"unknown attribute {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        var map = MapOf(target);
        if (!map.HasAttribute(attribute))
        {
            source.SendFailure($"{target.Name} has no attribute {Describe(attribute)}");
            return 0;
        }
        var value = map.GetBaseValue(attribute);
        source.SendSuccess($"{Describe(attribute)} base on {target.Name} is {value}");
        return (int)(value * scale);
    }

    //ResetBase restores the attribute base to the type default, maps to vanilla resetAttributeBase
    private static int ResetBase(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"unknown attribute {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        var map = MapOf(target);
        if (!map.ResetBaseValue(attribute))
        {
            source.SendFailure($"{target.Name} has no attribute {Describe(attribute)}");
            return 0;
        }
        var value = map.GetBaseValue(attribute);
        source.SendSuccess($"{Describe(attribute)} base on {target.Name} reset to {value}");
        return 1;
    }

    //SetBase overwrites the attribute base value, maps to vanilla setAttributeBase
    private static int SetBase(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"unknown attribute {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        if (!TryInstance(target, attribute, out var instance))
        {
            source.SendFailure($"{target.Name} has no attribute {Describe(attribute)}");
            return 0;
        }
        var value = DoubleArgumentType.GetDouble(context, "value");
        instance.SetBaseValue(value);
        source.SendSuccess($"{Describe(attribute)} base on {target.Name} set to {value}");
        return 1;
    }

    //AddModifier attaches a modifier, maps to vanilla addModifier
    private static int AddModifier(CommandContext<CommandSourceStack> context, AttributeOperation operation)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"unknown attribute {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        if (!TryInstance(target, attribute, out var instance))
        {
            source.SendFailure($"{target.Name} has no attribute {Describe(attribute)}");
            return 0;
        }
        var id = IdentifierArgument.GetId(context, "id");
        var value = DoubleArgumentType.GetDouble(context, "value");
        if (instance.HasModifier(id))
        {
            source.SendFailure($"modifier {id} already exists on {Describe(attribute)}");
            return 0;
        }
        instance.AddTransientModifier(new AttributeModifier(id, value, operation));
        source.SendSuccess($"added modifier {id} to {Describe(attribute)} of {target.Name}");
        return 1;
    }

    //RemoveModifier removes a modifier by id, maps to vanilla removeModifier
    private static int RemoveModifier(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"unknown attribute {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        if (!TryInstance(target, attribute, out var instance))
        {
            source.SendFailure($"{target.Name} has no attribute {Describe(attribute)}");
            return 0;
        }
        var id = IdentifierArgument.GetId(context, "id");
        if (!instance.RemoveModifier(id))
        {
            source.SendFailure($"no modifier {id} on {Describe(attribute)}");
            return 0;
        }
        source.SendSuccess($"removed modifier {id} from {Describe(attribute)} of {target.Name}");
        return 1;
    }

    //GetModifierValue reads back a single modifier's value, maps to vanilla getModifierValue
    //Vanilla reads the modifier's own amount without converting by operation
    private static int GetModifierValue(CommandContext<CommandSourceStack> context, double scale)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"unknown attribute {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        if (!TryInstance(target, attribute, out var instance))
        {
            source.SendFailure($"{target.Name} has no attribute {Describe(attribute)}");
            return 0;
        }
        var id = IdentifierArgument.GetId(context, "id");
        if (instance.GetModifier(id) is not { } modifier)
        {
            source.SendFailure($"no modifier {id} on {Describe(attribute)}");
            return 0;
        }
        var value = modifier.Amount;
        source.SendSuccess($"modifier {id} on {Describe(attribute)} of {target.Name} is {value}");
        return (int)(value * scale);
    }

    //Resolve parses the attribute argument; returns null when unregistered
    private static AttributeDef? Resolve(CommandContext<CommandSourceStack> context)
        => BuiltInRegistries.ATTRIBUTE.GetValue(ResourceArgument.GetResource(context, "attribute"));

    //MapOf gets the target entity's attribute map; players and level entities each hold one
    private static AttributeMap MapOf(CommandTarget target)
        => target.Player?.Attributes ?? target.WorldEntity!.Attributes;

    //TryInstance gets the attribute instance; returns false when the entity lacks this attribute
    private static bool TryInstance(CommandTarget target, AttributeDef attribute, out AttributeInstance instance)
    {
        instance = MapOf(target).GetInstance(attribute)!;
        return instance is not null;
    }

    private static string Describe(AttributeDef attribute) => attribute.DescriptionId;
}
