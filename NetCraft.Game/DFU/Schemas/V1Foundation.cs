namespace NetCraft.Game.DFU.Schemas;

using NetCraft.DataFixer;

using NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V1Foundation maps to vanilla V99 foundation schema
//Registers the recursive types ENTITY_TREE/ITEM_STACK/BLOCK_ENTITY/ENTITY etc. needed when constructing V1_21 segment schemas
//Simplified V99.registerTypes only registers the types actually referenced by the V1_21 segment; other types use Remainder placeholders
//Registers ENTITY/BLOCK_ENTITY as TaggedChoice so NamedEntityFix.GetChoiceType can find subtypes
public class V1Foundation : Schema
{
    public V1Foundation(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);

        //Recursive type registration: ENTITY as TaggedChoice("id",NamespacedString,entityTypes), simplified
        //Without the vanilla ENTITY_EQUIPMENT+custom_name to keep the end-to-end test path
        schema.RegisterType(true, References.Entity, () => DSL.TaggedChoice("id", NamespacedSchema.NamespacedString(), BuildTemplateMap(entityTypes)));

        //BLOCK_ENTITY registered as TaggedChoice("id",NamespacedString,blockEntityTypes), simplified
        //Without the vanilla components field for simplicity
        schema.RegisterType(true, References.BlockEntity, () => DSL.TaggedChoice("id", NamespacedSchema.NamespacedString(), BuildTemplateMap(blockEntityTypes)));

        //ITEM_STACK uses Remainder passthrough so Update/Get/Set/RenameField operate directly on CompoundTag
        schema.RegisterType(true, References.ItemStack, () => DSL.Remainder());

        //DATA_COMPONENTS uses Remainder passthrough
        schema.RegisterType(true, References.DataComponents, () => DSL.Remainder());

        //ENTITY_TREE/ENTITY_EQUIPMENT/PLAYER use Remainder placeholders
        schema.RegisterType(true, References.EntityTree, () => DSL.Remainder());
        schema.RegisterType(true, References.EntityEquipment, () => DSL.Remainder());
        schema.RegisterType(false, References.Player, () => DSL.Remainder());

        //Non-recursive base types are registered as ConstType placeholders
        schema.RegisterType(false, References.EntityName, () => DSL.ConstType(NamespacedSchema.NamespacedString()));
        schema.RegisterType(false, References.BlockName, () => DSL.ConstType(NamespacedSchema.NamespacedString()));
        schema.RegisterType(false, References.ItemName, () => DSL.ConstType(NamespacedSchema.NamespacedString()));
        schema.RegisterType(false, References.TextComponent, () => DSL.ConstType(DSL.String()));
        schema.RegisterType(false, References.BlockState, () => DSL.Remainder());

        //RECIPE uses a Remainder placeholder; V4312 references the recipe list in recipe_book when registering PLAYER
        schema.RegisterType(false, References.Recipe, () => DSL.Remainder());
    }

    //buildTemplateMap invokes all Func<TypeTemplate> dictionary entries into a TypeTemplate dictionary for DSL.TaggedChoice
    private static Dictionary<string, TypeTemplate> BuildTemplateMap(Dictionary<string, Func<TypeTemplate>> source)
    {
        var result = new Dictionary<string, TypeTemplate>();
        foreach (var kv in source)
        {
            result[kv.Key] = kv.Value();
        }
        return result;
    }

    //registerEntities returns all entity name placeholder templates referenced by the 21 fix classes in the V1_21 segment
    //MemoryExpiryDataFix uses villager; FixProjectileStoredItem/ProjectileStoredWeaponFix use trident/arrow/spectral_arrow
    //SaddleEquipmentSlotFix uses horse/skeleton_horse/zombie_horse/donkey/mule/camel/llama/trader_llama/pig/strider
    //piglin is registered as OptionalFields(Inventory) in both V1Foundation and V2505 to avoid the ENTITY TaggedChoice type differing between the two schemas
    //If the ENTITY types differ between the two schemas, NamedEntityFix's IfSame comparison fails and the fix rules cannot apply
    public override Dictionary<string, Func<TypeTemplate>> RegisterEntities(Schema schema)
    {
        var map = new Dictionary<string, Func<TypeTemplate>>();
        schema.RegisterSimple(map, "minecraft:villager");
        schema.Register(map, "minecraft:piglin", _ => DSL.OptionalFields(FixConstants.InventoryCarrierInventory, DSL.Remainder()));
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

    //registerBlockEntities returns all block entity name placeholder templates referenced by the V1_21 segment fix classes
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
