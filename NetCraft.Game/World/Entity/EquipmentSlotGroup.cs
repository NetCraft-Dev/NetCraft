using NetCraft.Codec;
using NetCraft.Network;

namespace NetCraft.Game.World.Entity;

//EquipmentSlotGroup 装备槽分组 对应原版 net.minecraft.world.entity.EquipmentSlotGroup
//一个分组覆盖一或多个槽位 属性修饰与谓词按分组筛选 声明顺序即网络 id
public enum EquipmentSlotGroup
{
    Any,
    Mainhand,
    Offhand,
    Hand,
    Feet,
    Legs,
    Chest,
    Head,
    Armor,
    Body
}

//EquipmentSlotGroups 装备槽分组的编解码与槽位判定
public static class EquipmentSlotGroups
{
    //Names 分组序列化名 下标与枚举值对应
    private static readonly string[] Names =
    {
        "any", "mainhand", "offhand", "hand", "feet", "legs", "chest", "head", "armor", "body"
    };

    //Codec 按序列化名编解码 对应原版 EquipmentSlotGroup.CODEC
    public static readonly Codec<EquipmentSlotGroup> Codec = Codecs.String.ComapFlatMap(
        name =>
        {
            for (var i = 0; i < Names.Length; i++)
                if (Names[i] == name) return DataResult<EquipmentSlotGroup>.Success((EquipmentSlotGroup)i);
            return DataResult<EquipmentSlotGroup>.Error(() => $"未知的装备槽分组: {name}");
        },
        group => Names[(int)group]);

    //StreamCodec 网络编解码 按分组 id 进出 对应原版 EquipmentSlotGroup.STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, EquipmentSlotGroup> StreamCodec =
        new EquipmentSlotGroupStreamCodec();

    //Test 该分组是否覆盖指定槽位 对应原版 EquipmentSlotGroup.test
    public static bool Test(this EquipmentSlotGroup group, EquipmentSlot slot) => group switch
    {
        EquipmentSlotGroup.Any => true,
        EquipmentSlotGroup.Mainhand => slot == EquipmentSlot.MAINHAND,
        EquipmentSlotGroup.Offhand => slot == EquipmentSlot.OFFHAND,
        EquipmentSlotGroup.Hand => slot is EquipmentSlot.MAINHAND or EquipmentSlot.OFFHAND,
        EquipmentSlotGroup.Feet => slot == EquipmentSlot.FEET,
        EquipmentSlotGroup.Legs => slot == EquipmentSlot.LEGS,
        EquipmentSlotGroup.Chest => slot == EquipmentSlot.CHEST,
        EquipmentSlotGroup.Head => slot == EquipmentSlot.HEAD,
        EquipmentSlotGroup.Armor => slot is EquipmentSlot.FEET or EquipmentSlot.LEGS or EquipmentSlot.CHEST or EquipmentSlot.HEAD,
        EquipmentSlotGroup.Body => slot == EquipmentSlot.BODY,
        _ => false
    };
}

//EquipmentSlotGroupStreamCodec 按分组 id 进出 对应原版 STREAM_CODEC
internal sealed class EquipmentSlotGroupStreamCodec : StreamCodec<RegistryFriendlyByteBuf, EquipmentSlotGroup>
{
    public EquipmentSlotGroup Decode(RegistryFriendlyByteBuf buf) => (EquipmentSlotGroup)buf.ReadVarInt();

    public void Encode(RegistryFriendlyByteBuf buf, EquipmentSlotGroup value) => buf.WriteVarInt((int)value);
}
