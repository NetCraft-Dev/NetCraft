using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Storage.Light;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLightUpdatePacket light update packet, maps to vanilla ClientboundLightUpdatePacket
//Fields: X(int), Z(int), LightData(ClientboundLightUpdatePacketData)
//x/z use VarInt, unlike the fixed Int in the chunk packet
public sealed record ClientboundLightUpdatePacket(int X, int Z, ClientboundLightUpdatePacketData LightData) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLightUpdatePacket> StreamCodec { get; } = new LightUpdateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLightUpdate;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLightUpdatePacket(this);

    //Builds an incremental send packet from the light engine, maps to the vanilla constructor
    public ClientboundLightUpdatePacket(ChunkPos pos, LevelLightEngine lightEngine,
        byte[]? skyFilter = null, byte[]? blockFilter = null)
        : this(pos.X, pos.Z, new ClientboundLightUpdatePacketData(pos, lightEngine, skyFilter, blockFilter)) { }

    private sealed class LightUpdateCodec : StreamCodec<FriendlyByteBuf, ClientboundLightUpdatePacket>
    {
        public ClientboundLightUpdatePacket Decode(FriendlyByteBuf buf)
        {
            var x = buf.ReadVarInt();
            var z = buf.ReadVarInt();
            return new ClientboundLightUpdatePacket(x, z, ClientboundLightUpdatePacketData.Read(buf));
        }

        public void Encode(FriendlyByteBuf buf, ClientboundLightUpdatePacket value)
        {
            buf.WriteVarInt(value.X);
            buf.WriteVarInt(value.Z);
            value.LightData.Write(buf);
        }
    }
}
