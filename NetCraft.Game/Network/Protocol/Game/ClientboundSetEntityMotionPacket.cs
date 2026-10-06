using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetEntityMotionPacket entity motion packet, maps to vanilla ClientboundSetEntityMotionPacket
//Fields: id VarInt, movement Vec3 using low-precision quantized encoding, maps to vanilla Vec3.LP_STREAM_CODEC
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
