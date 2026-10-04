using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundContainerSetDataPacket 容器数据设置包对应原版 ClientboundContainerSetDataPacket
//字段 ContainerId(VarInt) Id(Short) Value(Short) 对齐原版三个数字字段
public sealed record ClientboundContainerSetDataPacket(int ContainerId, int Id, int Value) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundContainerSetDataPacket> StreamCodec { get; } = new ContainerSetDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundContainerSetData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleContainerSetData(this);

    private sealed class ContainerSetDataCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundContainerSetDataPacket>
    {
        public ClientboundContainerSetDataPacket Decode(RegistryFriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadShort(), buf.ReadShort());

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundContainerSetDataPacket value)
        {
            buf.WriteVarInt(value.ContainerId);
            buf.WriteShort((short)value.Id);
            buf.WriteShort((short)value.Value);
        }
    }
}
