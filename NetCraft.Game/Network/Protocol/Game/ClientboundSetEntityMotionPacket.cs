using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetEntityMotionPacket 实体动量包对应原版 ClientboundSetEntityMotionPacket
//字段 id VarInt movement Vec3 走低精度量化编码 对应原版 Vec3.LP_STREAM_CODEC
public sealed record ClientboundSetEntityMotionPacket(int Id, Vec3 Movement) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetEntityMotionPacket> StreamCodec { get; } = new SetEntityMotionCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetEntityMotion;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetEntityMotion(this);

    private sealed class SetEntityMotionCodec : StreamCodec<FriendlyByteBuf, ClientboundSetEntityMotionPacket>
    {
        public ClientboundSetEntityMotionPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadLpVec3());

        public void Encode(FriendlyByteBuf buf, ClientboundSetEntityMotionPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteLpVec3(value.Movement);
        }
    }
}
