using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundForgetLevelChunkPacket forget level chunk packet, maps to vanilla ClientboundForgetLevelChunkPacket
//Field: Pos(ChunkPos); the network format is a single long (x|z<<32)
public sealed record ClientboundForgetLevelChunkPacket(ChunkPos Pos) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundForgetLevelChunkPacket> StreamCodec { get; } = new ForgetLevelChunkCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundForgetLevelChunk;

    public void Handle(ClientGamePacketListener handler) => handler.HandleForgetLevelChunk(this);

    private sealed class ForgetLevelChunkCodec : StreamCodec<FriendlyByteBuf, ClientboundForgetLevelChunkPacket>
    {
        public ClientboundForgetLevelChunkPacket Decode(FriendlyByteBuf buf)
            => new(ChunkPos.Unpack(buf.ReadLong()));

        public void Encode(FriendlyByteBuf buf, ClientboundForgetLevelChunkPacket value)
            => buf.WriteLong(ChunkPos.Pack(value.Pos.X, value.Pos.Z));
    }
}
