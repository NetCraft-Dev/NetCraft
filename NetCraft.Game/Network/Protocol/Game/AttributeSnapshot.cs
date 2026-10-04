using NetCraft.Registry;
//属性类型自带命名空间 与环境属性里同名类型分开 这里只取需要的三个
using AttributeDef = NetCraft.Registry.EntityAttribute.Attribute;
using AttributeModifier = NetCraft.Registry.EntityAttribute.AttributeModifier;
using AttributeOperation = NetCraft.Registry.EntityAttribute.AttributeOperation;

namespace NetCraft.Game.Network.Protocol.Game;

//AttributeSnapshot 单条属性快照 对应原版 ClientboundUpdateAttributesPacket.AttributeSnapshot
//线上传的是属性引用 基值 与该属性上的全部修饰符 属性走注册表 id 不走 Identifier
public sealed record AttributeSnapshot(AttributeDef Attribute, double Base,
    IReadOnlyList<AttributeModifier> Modifiers)
{
    //Write 按原版 AttributeSnapshot.STREAM_CODEC 写 属性写注册表 id 修饰符写 id 数值 运算
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

    //Read 按原版解码 属性 id 越界直接抛 对应原版 holderRegistry 的 byId 行为
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
