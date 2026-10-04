namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V4067对应原版net.minecraft.util.datafix.schemas.V4067
//1.21移除boat/chest_boat统一实体改为9种木材boat+9种带箱boat
public class V4067 : NamespacedSchema
{
    public V4067(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override Dictionary<string, Func<TypeTemplate>> RegisterEntities(Schema schema)
    {
        var map = base.RegisterEntities(schema);
        map.Remove("minecraft:boat");
        map.Remove("minecraft:chest_boat");
        RegisterSimple(map, "minecraft:oak_boat");
        RegisterSimple(map, "minecraft:spruce_boat");
        RegisterSimple(map, "minecraft:birch_boat");
        RegisterSimple(map, "minecraft:jungle_boat");
        RegisterSimple(map, "minecraft:acacia_boat");
        RegisterSimple(map, "minecraft:cherry_boat");
        RegisterSimple(map, "minecraft:dark_oak_boat");
        RegisterSimple(map, "minecraft:mangrove_boat");
        RegisterSimple(map, "minecraft:bamboo_raft");
        RegisterChestBoat(map, "minecraft:oak_chest_boat");
        RegisterChestBoat(map, "minecraft:spruce_chest_boat");
        RegisterChestBoat(map, "minecraft:birch_chest_boat");
        RegisterChestBoat(map, "minecraft:jungle_chest_boat");
        RegisterChestBoat(map, "minecraft:acacia_chest_boat");
        RegisterChestBoat(map, "minecraft:cherry_chest_boat");
        RegisterChestBoat(map, "minecraft:dark_oak_chest_boat");
        RegisterChestBoat(map, "minecraft:mangrove_chest_boat");
        RegisterChestBoat(map, "minecraft:bamboo_chest_raft");
        return map;
    }

    //registerChestBoat注册带箱船模板带Items字段列表对应原版registerChestBoat
    private void RegisterChestBoat(Dictionary<string, Func<TypeTemplate>> map, string id)
        => Register(map, id, _ => DSL.OptionalFields(FixConstants.ContainerHelperItems, DSL.List(References.ItemStack.In(this))));
}
