using NetCraft.Codec;
using NetCraft.Network;

namespace NetCraft.Game.World.Entity;

//EquipmentSlotGroup equipment slot group, maps to vanilla net.minecraft.world.entity.EquipmentSlotGroup
//A group covers one or more slots, attribute modifiers and predicates filter by group, the declaration order is the network id
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

//EquipmentSlotGroups codecs and slot tests for equipment slot groups
public static class EquipmentSlotGroups
{
    //Names group serialized names, the index corresponds to the enum value
    private static readonly string[] Names =
    {
        "any", "mainhand", "offhand", "hand", "feet", "legs", "chest", "head", "armor", "body"
    };

    //Codec encodes by serialized name, maps to vanilla EquipmentSlotGroup.CODEC
    public static readonly Codec<EquipmentSlotGroup> Codec = Codecs.String.ComapFlatMap(
        name =>
        {
            for (var i = 0; i < Names.Length; i++)
                if (Names[i] == name) return DataResult<EquipmentSlotGroup>.Success((EquipmentSlotGroup)i);
            return DataResult<EquipmentSlotGroup>.Error(() => $"unknown equipment slot group: {name}");
        },
        group => Names[(int)group]);

    //StreamCodec network codec, goes in and out by group id, maps to vanilla EquipmentSlotGroup.STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, EquipmentSlotGroup> StreamCodec =
        new EquipmentSlotGroupStreamCodec();

    //Test whether the group covers the given slot, maps to vanilla EquipmentSlotGroup.test
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

//EquipmentSlotGroupStreamCodec goes in and out by group id, maps to vanilla STREAM_CODEC
internal sealed class EquipmentSlotGroupStreamCodec : StreamCodec<RegistryFriendlyByteBuf, EquipmentSlotGroup>
{
    public EquipmentSlotGroup Decode(RegistryFriendlyByteBuf buf) => (EquipmentSlotGroup)buf.ReadVarInt();

    public void Encode(RegistryFriendlyByteBuf buf, EquipmentSlotGroup value) => buf.WriteVarInt((int)value);
}
