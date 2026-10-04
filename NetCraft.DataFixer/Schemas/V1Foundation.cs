namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V1Foundation对应原版V99基础Schema
//注册V1_21段Schema构造时需要的递归类型ENTITY_TREE/ITEM_STACK/BLOCK_ENTITY/ENTITY等
//简化V99.registerTypes只注册V1_21段实际引用的类型其他类型用Remainder占位
//注册ENTITY/BLOCK_ENTITY为TaggedChoice让NamedEntityFix.GetChoiceType能找到子类型
public class V1Foundation : Schema
{
    public V1Foundation(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);

        //递归类型注册ENTITY为TaggedChoice("id",NamespacedString,entityTypes)简化版
        //不带原版ENTITY_EQUIPMENT+custom_name简化端到端测试路径
        schema.RegisterType(true, References.Entity, () => DSL.TaggedChoice("id", NamespacedSchema.NamespacedString(), BuildTemplateMap(entityTypes)));

        //BLOCK_ENTITY注册为TaggedChoice("id",NamespacedString,blockEntityTypes)简化版
        //不带原版components字段简化
        schema.RegisterType(true, References.BlockEntity, () => DSL.TaggedChoice("id", NamespacedSchema.NamespacedString(), BuildTemplateMap(blockEntityTypes)));

        //ITEM_STACK用Remainder透传让Update/Get/Set/RenameField直接操作CompoundTag
        schema.RegisterType(true, References.ItemStack, () => DSL.Remainder());

        //DATA_COMPONENTS用Remainder透传
        schema.RegisterType(true, References.DataComponents, () => DSL.Remainder());

        //ENTITY_TREE/ENTITY_EQUIPMENT/PLAYER用Remainder占位
        schema.RegisterType(true, References.EntityTree, () => DSL.Remainder());
        schema.RegisterType(true, References.EntityEquipment, () => DSL.Remainder());
        schema.RegisterType(false, References.Player, () => DSL.Remainder());

        //非递归基础类型注册为ConstType占位
        schema.RegisterType(false, References.EntityName, () => DSL.ConstType(NamespacedSchema.NamespacedString()));
        schema.RegisterType(false, References.BlockName, () => DSL.ConstType(NamespacedSchema.NamespacedString()));
        schema.RegisterType(false, References.ItemName, () => DSL.ConstType(NamespacedSchema.NamespacedString()));
        schema.RegisterType(false, References.TextComponent, () => DSL.ConstType(DSL.String()));
        schema.RegisterType(false, References.BlockState, () => DSL.Remainder());
    }

    //buildTemplateMap把Func<TypeTemplate>字典全部invoke为TypeTemplate字典供DSL.TaggedChoice使用
    private static Dictionary<string, TypeTemplate> BuildTemplateMap(Dictionary<string, Func<TypeTemplate>> source)
    {
        var result = new Dictionary<string, TypeTemplate>();
        foreach (var kv in source)
        {
            result[kv.Key] = kv.Value();
        }
        return result;
    }

    //registerEntities返回V1_21段21个Fix类引用的全部实体名占位模板
    //MemoryExpiryDataFix用villager FixProjectileStoredItem/ProjectileStoredWeaponFix用trident/arrow/spectral_arrow
    //SaddleEquipmentSlotFix用horse/skeleton_horse/zombie_horse/donkey/mule/camel/llama/trader_llama/pig/strider
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

    //registerBlockEntities返回V1_21段Fix类引用的全部方块实体名占位模板
    //JukeboxTicksSinceSongStartedFix用jukebox TrialSpawnerConfigFix/TrialSpawnerConfigInRegistryFix用trial_spawner
    //DecoratedPotFieldRenameFix用decorated_pot
    public override Dictionary<string, Func<TypeTemplate>> RegisterBlockEntities(Schema schema)
    {
        var map = new Dictionary<string, Func<TypeTemplate>>();
        schema.RegisterSimple(map, "minecraft:jukebox");
        schema.RegisterSimple(map, "minecraft:trial_spawner");
        schema.RegisterSimple(map, "minecraft:decorated_pot");
        return map;
    }
}
