using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Registry;
//属性相关类型自带命名空间 这里只取需要的几个名字
using AttributeInstance = NetCraft.Registry.EntityAttribute.AttributeInstance;
using AttributeMap = NetCraft.Registry.EntityAttribute.AttributeMap;
using AttributeDef = NetCraft.Registry.EntityAttribute.Attribute;
using AttributeModifier = NetCraft.Registry.EntityAttribute.AttributeModifier;
using AttributeOperation = NetCraft.Registry.EntityAttribute.AttributeOperation;

namespace NetCraft.Game.Commands;

//AttributeCommand attribute 命令对应原版 net.minecraft.server.commands.AttributeCommand
//attribute <target> <attribute> 后接 get / base / modifier 三条分支
//base 含 set/get/reset modifier 含 add/remove/value get 全量对齐原版
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

    //GetValue 回读属性最终值 对应原版 getAttributeValue
    private static int GetValue(CommandContext<CommandSourceStack> context, double scale)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"未知属性 {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        var map = MapOf(target);
        if (!map.HasAttribute(attribute))
        {
            source.SendFailure($"{target.Name} 没有属性 {Describe(attribute)}");
            return 0;
        }
        var value = map.GetValue(attribute);
        source.SendSuccess($"{Describe(attribute)} 在 {target.Name} 上的值为 {value}");
        return (int)(value * scale);
    }

    //GetBase 回读属性基值 对应原版 getAttributeBase
    private static int GetBase(CommandContext<CommandSourceStack> context, double scale)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"未知属性 {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        var map = MapOf(target);
        if (!map.HasAttribute(attribute))
        {
            source.SendFailure($"{target.Name} 没有属性 {Describe(attribute)}");
            return 0;
        }
        var value = map.GetBaseValue(attribute);
        source.SendSuccess($"{Describe(attribute)} 在 {target.Name} 上的基值为 {value}");
        return (int)(value * scale);
    }

    //ResetBase 把属性基值恢复为该类型默认值 对应原版 resetAttributeBase
    private static int ResetBase(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"未知属性 {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        var map = MapOf(target);
        if (!map.ResetBaseValue(attribute))
        {
            source.SendFailure($"{target.Name} 没有属性 {Describe(attribute)}");
            return 0;
        }
        var value = map.GetBaseValue(attribute);
        source.SendSuccess($"{Describe(attribute)} 在 {target.Name} 上的基值已重置为 {value}");
        return 1;
    }

    //SetBase 改写属性基值 对应原版 setAttributeBase
    private static int SetBase(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"未知属性 {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        if (!TryInstance(target, attribute, out var instance))
        {
            source.SendFailure($"{target.Name} 没有属性 {Describe(attribute)}");
            return 0;
        }
        var value = DoubleArgumentType.GetDouble(context, "value");
        instance.SetBaseValue(value);
        source.SendSuccess($"{Describe(attribute)} 在 {target.Name} 上的基值已设为 {value}");
        return 1;
    }

    //AddModifier 挂一条修饰符 对应原版 addModifier
    private static int AddModifier(CommandContext<CommandSourceStack> context, AttributeOperation operation)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"未知属性 {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        if (!TryInstance(target, attribute, out var instance))
        {
            source.SendFailure($"{target.Name} 没有属性 {Describe(attribute)}");
            return 0;
        }
        var id = IdentifierArgument.GetId(context, "id");
        var value = DoubleArgumentType.GetDouble(context, "value");
        if (instance.HasModifier(id))
        {
            source.SendFailure($"{Describe(attribute)} 上已存在修饰符 {id}");
            return 0;
        }
        instance.AddTransientModifier(new AttributeModifier(id, value, operation));
        source.SendSuccess($"已为 {target.Name} 的 {Describe(attribute)} 添加修饰符 {id}");
        return 1;
    }

    //RemoveModifier 按 id 摘掉修饰符 对应原版 removeModifier
    private static int RemoveModifier(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"未知属性 {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        if (!TryInstance(target, attribute, out var instance))
        {
            source.SendFailure($"{target.Name} 没有属性 {Describe(attribute)}");
            return 0;
        }
        var id = IdentifierArgument.GetId(context, "id");
        if (!instance.RemoveModifier(id))
        {
            source.SendFailure($"{Describe(attribute)} 上没有修饰符 {id}");
            return 0;
        }
        source.SendSuccess($"已移除 {target.Name} 的 {Describe(attribute)} 上的修饰符 {id}");
        return 1;
    }

    //GetModifierValue 回读单条修饰符的数值 对应原版 getModifierValue
    //原版取的就是修饰符自身的 amount 不随运算方式换算
    private static int GetModifierValue(CommandContext<CommandSourceStack> context, double scale)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetSingleTarget(context, "target");
        if (Resolve(context) is not { } attribute)
        {
            source.SendFailure($"未知属性 {ResourceArgument.GetResource(context, "attribute")}");
            return 0;
        }
        if (!TryInstance(target, attribute, out var instance))
        {
            source.SendFailure($"{target.Name} 没有属性 {Describe(attribute)}");
            return 0;
        }
        var id = IdentifierArgument.GetId(context, "id");
        if (instance.GetModifier(id) is not { } modifier)
        {
            source.SendFailure($"{Describe(attribute)} 上没有修饰符 {id}");
            return 0;
        }
        var value = modifier.Amount;
        source.SendSuccess($"{Describe(attribute)} 在 {target.Name} 上的修饰符 {id} 的值为 {value}");
        return (int)(value * scale);
    }

    //Resolve 解析属性参数 未注册返回 null
    private static AttributeDef? Resolve(CommandContext<CommandSourceStack> context)
        => BuiltInRegistries.ATTRIBUTE.GetValue(ResourceArgument.GetResource(context, "attribute"));

    //MapOf 取目标实体的属性表 玩家与关卡实体各自持有
    private static AttributeMap MapOf(CommandTarget target)
        => target.Player?.Attributes ?? target.WorldEntity!.Attributes;

    //TryInstance 取属性实例 该实体没有此属性返回 false
    private static bool TryInstance(CommandTarget target, AttributeDef attribute, out AttributeInstance instance)
    {
        instance = MapOf(target).GetInstance(attribute)!;
        return instance is not null;
    }

    private static string Describe(AttributeDef attribute) => attribute.DescriptionId;
}
