using NetCraft.Registry;
//The attribute types have their own namespace, kept separate from the same-named types in the environment attributes; only the three needed ones are taken here
using AttributeDef = NetCraft.Registry.EntityAttribute.Attribute;
using AttributeModifier = NetCraft.Registry.EntityAttribute.AttributeModifier;
using AttributeOperation = NetCraft.Registry.EntityAttribute.AttributeOperation;

namespace NetCraft.Game.Network.Protocol.Game;

//AttributeSnapshot a single attribute snapshot, maps to vanilla ClientboundUpdateAttributesPacket.AttributeSnapshot
//On the wire this carries the attribute reference, base value, and all modifiers on the attribute; the attribute uses a registry id, not an Identifier
public sealed record AttributeSnapshot(AttributeDef Attribute, double Base,
    IReadOnlyList<AttributeModifier> Modifiers)
{
    //Write follows vanilla AttributeSnapshot.STREAM_CODEC; the attribute writes a registry id, modifiers write id, amount, operation
    public void Write(FriendlyByteBuf buf)
    {
        buf.WriteVarInt(BuiltInRegistries.ATTRIBUTE.GetIdOrThrow(Attribute));
        buf.WriteDouble(Base);
        buf.WriteVarInt(Modifiers.Count);
        foreach (var modifier in Modifiers)
        {
            buf.WriteIdentifier(modifier.Id);
            buf.WriteDouble(modifier.Amount);
            buf.WriteVarInt((int)modifier.Operation);
        }
    }

    //Read decodes like vanilla; an out-of-range attribute id throws immediately, matching vanilla holderRegistry.byId behavior
    public static AttributeSnapshot Read(FriendlyByteBuf buf)
    {
        var attribute = BuiltInRegistries.ATTRIBUTE.ByIdOrThrow(buf.ReadVarInt());
        var baseValue = buf.ReadDouble();
        var count = buf.ReadVarInt();
        var modifiers = new AttributeModifier[count];
        for (var i = 0; i < count; i++)
            modifiers[i] = new AttributeModifier(buf.ReadIdentifier(), buf.ReadDouble(),
                (AttributeOperation)buf.ReadVarInt());
        return new AttributeSnapshot(attribute, baseValue, modifiers);
    }
}
