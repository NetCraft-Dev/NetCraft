namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V1Foundation maps to the vanilla V99 base Schema
//registers the recursive types needed when constructing the V1_21 segment Schema: ENTITY_TREE/ITEM_STACK/BLOCK_ENTITY/ENTITY etc.
//simplifies V99.registerTypes: only registers types actually referenced by the V1_21 segment; others use Remainder placeholders
//registers ENTITY/BLOCK_ENTITY as TaggedChoice so NamedEntityFix.GetChoiceType can find the child types
public class V1Foundation : Schema
{
    public V1Foundation(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);

        //recursive type: registers ENTITY as a simplified TaggedChoice("id",NamespacedString,entityTypes)
        //omits the vanilla ENTITY_EQUIPMENT+custom_name to simplify the end-to-end test path
        schema.RegisterType(true, References.Entity, () => DSL.TaggedChoice("id", NamespacedSchema.NamespacedString(), BuildTemplateMap(entityTypes)));

        //registers BLOCK_ENTITY as a simplified TaggedChoice("id",NamespacedString,blockEntityTypes)
        //omits the vanilla components field to simplify
        schema.RegisterType(true, References.BlockEntity, () => DSL.TaggedChoice("id", NamespacedSchema.NamespacedString(), BuildTemplateMap(blockEntityTypes)));

        //ITEM_STACK passes through as Remainder so Update/Get/Set/RenameField operate directly on CompoundTag
        schema.RegisterType(true, References.ItemStack, () => DSL.Remainder());

        //DATA_COMPONENTS passes through as Remainder
        schema.RegisterType(true, References.DataComponents, () => DSL.Remainder());

        //ENTITY_TREE/ENTITY_EQUIPMENT/PLAYER use Remainder placeholders
        schema.RegisterType(true, References.EntityTree, () => DSL.Remainder());
        schema.RegisterType(true, References.EntityEquipment, () => DSL.Remainder());
        schema.RegisterType(false, References.Player, () => DSL.Remainder());

        //non-recursive base types are registered as ConstType placeholders
        schema.RegisterType(false, References.EntityName, () => DSL.ConstType(NamespacedSchema.NamespacedString()));
        schema.RegisterType(false, References.BlockName, () => DSL.ConstType(NamespacedSchema.NamespacedString()));
        schema.RegisterType(false, References.ItemName, () => DSL.ConstType(NamespacedSchema.NamespacedString()));
        schema.RegisterType(false, References.TextComponent, () => DSL.ConstType(DSL.String()));
        schema.RegisterType(false, References.BlockState, () => DSL.Remainder());
    }

    //buildTemplateMap invokes every Func<TypeTemplate> in the dictionary into a TypeTemplate dictionary for DSL.TaggedChoice
    private static Dictionary<string, TypeTemplate> BuildTemplateMap(Dictionary<string, Func<TypeTemplate>> source)
    {
        var result = new Dictionary<string, TypeTemplate>();
        foreach (var kv in source)
        {
            result[kv.Key] = kv.Value();
        }
        return result;
    }

    //registerEntities returns placeholder templates for all entity names referenced by the 21 Fix classes in the V1_21 segment
    //MemoryExpiryDataFix uses villager; FixProjectileStoredItem/ProjectileStoredWeaponFix use trident/arrow/spectral_arrow
    //SaddleEquipmentSlotFix uses horse/skeleton_horse/zombie_horse/donkey/mule/camel/llama/trader_llama/pig/strider
    public override Dictionary<string, Func<TypeTemplate>> RegisterEntities(Schema schema)
    {
        var map = new Dictionary<string, Func<TypeTemplate>>();
        schema.RegisterSimple(map, "minecraft:villager");
        schema.RegisterSimple(map, "minecraft:piglin");
        schema.RegisterSimple(map, "minecraft:trident");
        schema.RegisterSimple(map, "minecraft:arrow");
        schema.RegisterSimple(map, "minecraft:spectral_arrow");
        schema.RegisterSimple(map, "minecraft:horse");
        schema.RegisterSimple(map, "minecraft:skeleton_horse");
        schema.RegisterSimple(map, "minecraft:zombie_horse");
        schema.RegisterSimple(map, "minecraft:donkey");
        schema.RegisterSimple(map, "minecraft:mule");
        schema.RegisterSimple(map, "minecraft:camel");
        schema.RegisterSimple(map, "minecraft:llama");
        schema.RegisterSimple(map, "minecraft:trader_llama");
        schema.RegisterSimple(map, "minecraft:pig");
        schema.RegisterSimple(map, "minecraft:strider");
        return map;
    }

    //registerBlockEntities returns placeholder templates for all block entity names referenced by the V1_21 segment Fix classes
    //JukeboxTicksSinceSongStartedFix uses jukebox; TrialSpawnerConfigFix/TrialSpawnerConfigInRegistryFix use trial_spawner
    //DecoratedPotFieldRenameFix uses decorated_pot
    public override Dictionary<string, Func<TypeTemplate>> RegisterBlockEntities(Schema schema)
    {
        var map = new Dictionary<string, Func<TypeTemplate>>();
        schema.RegisterSimple(map, "minecraft:jukebox");
        schema.RegisterSimple(map, "minecraft:trial_spawner");
        schema.RegisterSimple(map, "minecraft:decorated_pot");
        return map;
    }
}
