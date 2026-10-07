using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//tooltip display component fix, maps to vanilla TooltipDisplayComponentFix
//1.21.4 moves the show_in_tooltip field from each data component into minecraft:tooltip_display for unified handling
//can_place_on/can_break are unwrapped into the predicates list; trim/unbreakable/dyed_color/attribute_modifiers etc. drop show_in_tooltip
//hide_tooltip and hide_additional_tooltip are merged into minecraft:tooltip_display
public class TooltipDisplayComponentFix : DataFix
{
    //the 18 data component IDs with additional_tooltip semantics
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

    //fix first accumulates hiddenTooltips via Set, then handles can_place_on/can_break and the 7 remaining components, finally building tooltip_display
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

    //fixSimpleComponent handles components that only remove show_in_tooltip, maps to vanilla fixSimpleComponent
    private static Dynamic<object> FixSimpleComponent(Dynamic<object> remainder, string componentId, HashSet<string> hiddenTooltips)
        => FixRemainderComponent(remainder, componentId, hiddenTooltips, c => c);

    //fixComponentAndUnwrap removes show_in_tooltip and unwraps to the fieldName sub-value, maps to vanilla fixComponentAndUnwrap
    private static Dynamic<object> FixComponentAndUnwrap(Dynamic<object> remainder, string componentId, string fieldName, HashSet<string> hiddenTooltips)
        => FixRemainderComponent(remainder, componentId, hiddenTooltips,
            component => DataFixUtils.OrElse(component.Get(fieldName).Result(), component));

    //fixRemainderComponent uniformly handles the show_in_tooltip field, adding it to hiddenTooltips then applying fixer
    private static Dynamic<object> FixRemainderComponent(Dynamic<object> remainder, string componentId, HashSet<string> hiddenTooltips, Func<Dynamic<object>, Dynamic<object>> fixer)
        => remainder.Update(componentId, component =>
        {
            var showInTooltip = component.Get("show_in_tooltip").AsBoolean(true);
            if (!showInTooltip)
                hiddenTooltips.Add(componentId);
            return fixer(component.Remove("show_in_tooltip"));
        });

    //fixAdventureModePredicate uses WriteAndReadTypedOrThrow to rewrite the component into a Dynamic containing only predicates
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
