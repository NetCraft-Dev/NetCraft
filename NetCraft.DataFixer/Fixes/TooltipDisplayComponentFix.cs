using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//tooltip显示组件修复对应原版TooltipDisplayComponentFix
//1.21.4把show_in_tooltip字段从各数据组件移到minecraft:tooltip_display统一管理
//can_place_on/can_break解包为predicates列表trim/unbreakable/dyed_color/attribute_modifiers等移除show_in_tooltip
//hide_tooltip和hide_additional_tooltip合并到minecraft:tooltip_display
public class TooltipDisplayComponentFix : DataFix
{
    //18个有additional_tooltip语义的数据组件ID
    private static readonly List<string> ConvertedAdditionalTooltipTypes = new()
    {
        "minecraft:banner_patterns",
        "minecraft:bees",
        "minecraft:block_entity_data",
        "minecraft:block_state",
        "minecraft:bundle_contents",
        "minecraft:charged_projectiles",
        "minecraft:container",
        "minecraft:container_loot",
        "minecraft:firework_explosion",
        "minecraft:fireworks",
        "minecraft:instrument",
        "minecraft:map_id",
        "minecraft:painting/variant",
        "minecraft:pot_decorations",
        "minecraft:potion_contents",
        "minecraft:tropical_fish/pattern",
        "minecraft:written_book_content"
    };

    public TooltipDisplayComponentFix(Schema outputSchema) : base(outputSchema, true) { }

    protected override TypeRewriteRule MakeRule()
    {
        var componentsType = GetInputSchema().GetType(References.DataComponents);
        var newComponentsType = GetOutputSchema().GetType(References.DataComponents);
        var canPlaceOnFinder = componentsType.FindField("minecraft:can_place_on");
        var canBreakFinder = componentsType.FindField("minecraft:can_break");
        var newCanPlaceOnType = newComponentsType.FindFieldType("minecraft:can_place_on");
        var newCanBreakType = newComponentsType.FindFieldType("minecraft:can_break");
        return FixTypeEverywhereTyped("TooltipDisplayComponentFix",
            componentsType, newComponentsType,
            typed => Fix(typed, canPlaceOnFinder, canBreakFinder, newCanPlaceOnType, newCanBreakType));
    }

    //fix先用Set累积hiddenTooltips再依次处理can_place_on/can_break和7个剩余组件最后构造tooltip_display
    private static Typed<object> Fix(Typed<object> typed,
        OpticFinder<object> canPlaceOnFinder, OpticFinder<object> canBreakFinder,
        T.Type<object> newCanPlaceOnType, T.Type<object> newCanBreakType)
    {
        var hiddenTooltips = new HashSet<string>();
        var afterCanPlaceOn = FixAdventureModePredicate(typed, canPlaceOnFinder, newCanPlaceOnType, "minecraft:can_place_on", hiddenTooltips);
        var afterCanBreak = FixAdventureModePredicate(afterCanPlaceOn, canBreakFinder, newCanBreakType, "minecraft:can_break", hiddenTooltips);
        return afterCanBreak.Update(DSL.RemainderFinder(), remainder =>
        {
            var r = FixSimpleComponent(remainder, "minecraft:trim", hiddenTooltips);
            r = FixSimpleComponent(r, "minecraft:unbreakable", hiddenTooltips);
            r = FixComponentAndUnwrap(r, "minecraft:dyed_color", "rgb", hiddenTooltips);
            r = FixComponentAndUnwrap(r, "minecraft:attribute_modifiers", "modifiers", hiddenTooltips);
            r = FixComponentAndUnwrap(r, "minecraft:enchantments", "levels", hiddenTooltips);
            r = FixComponentAndUnwrap(r, "minecraft:stored_enchantments", "levels", hiddenTooltips);
            r = FixComponentAndUnwrap(r, "minecraft:jukebox_playable", "song", hiddenTooltips);

            var hideTooltip = r.Get("minecraft:hide_tooltip").Result().IsPresent;
            var r2 = r.Remove("minecraft:hide_tooltip");
            var hideAdditionalTooltip = r2.Get("minecraft:hide_additional_tooltip").Result().IsPresent;
            var r3 = r2.Remove("minecraft:hide_additional_tooltip");
            if (hideAdditionalTooltip)
            {
                foreach (var componentId in ConvertedAdditionalTooltipTypes)
                {
                    if (r3.Get(componentId).Result().IsPresent)
                        hiddenTooltips.Add(componentId);
                }
            }
            if (hiddenTooltips.Count == 0 && !hideTooltip)
                return r3;
            var entries = new[]
            {
                new Pair<Dynamic<object>, Dynamic<object>>(r3.CreateString("hide_tooltip"), r3.CreateBoolean(hideTooltip)),
                new Pair<Dynamic<object>, Dynamic<object>>(r3.CreateString("hidden_components"),
                    r3.CreateList(hiddenTooltips.Select(r3.CreateString)))
            };
            return r3.Set("minecraft:tooltip_display", r3.CreateMap(entries));
        });
    }

    //fixSimpleComponent处理只移除show_in_tooltip的组件对应原版fixSimpleComponent
    private static Dynamic<object> FixSimpleComponent(Dynamic<object> remainder, string componentId, HashSet<string> hiddenTooltips)
        => FixRemainderComponent(remainder, componentId, hiddenTooltips, c => c);

    //fixComponentAndUnwrap移除show_in_tooltip并解包到fieldName子值对应原版fixComponentAndUnwrap
    private static Dynamic<object> FixComponentAndUnwrap(Dynamic<object> remainder, string componentId, string fieldName, HashSet<string> hiddenTooltips)
        => FixRemainderComponent(remainder, componentId, hiddenTooltips,
            component => DataFixUtils.OrElse(component.Get(fieldName).Result(), component));

    //fixRemainderComponent统一处理show_in_tooltip字段累加到hiddenTooltips后应用fixer
    private static Dynamic<object> FixRemainderComponent(Dynamic<object> remainder, string componentId, HashSet<string> hiddenTooltips, Func<Dynamic<object>, Dynamic<object>> fixer)
        => remainder.Update(componentId, component =>
        {
            var showInTooltip = component.Get("show_in_tooltip").AsBoolean(true);
            if (!showInTooltip)
                hiddenTooltips.Add(componentId);
            return fixer(component.Remove("show_in_tooltip"));
        });

    //fixAdventureModePredicate用WriteAndReadTypedOrThrow把component重写为只含predicates的Dynamic
    private static Typed<object> FixAdventureModePredicate(Typed<object> typedComponents,
        OpticFinder<object> componentFinder, T.Type<object> newType, string componentId, HashSet<string> hiddenTooltips)
        => typedComponents.UpdateTyped(componentFinder, newType, typedComponent =>
            DataFixUtils.WriteAndReadTypedOrThrow<object, object>(typedComponent, newType, component =>
            {
                var predicates = component.Get("predicates");
                if (!predicates.Result().IsPresent)
                    return component;
                var showInTooltip = component.Get("show_in_tooltip").AsBoolean(true);
                if (!showInTooltip)
                    hiddenTooltips.Add(componentId);
                return predicates.Result().Get();
            }));
}
