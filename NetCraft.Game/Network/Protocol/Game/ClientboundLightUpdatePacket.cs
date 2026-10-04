using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Storage.Light;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLightUpdatePacket 光照更新包对应原版 ClientboundLightUpdatePacket
//字段 X(int) Z(int) LightData(ClientboundLightUpdatePacketData)
//x/z 走 VarInt 与区块包固定 Int 不同
public sealed record ClientboundLightUpdatePacket(int X, int Z, ClientboundLightUpdatePacketData LightData) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLightUpdatePacket> StreamCodec { get; } = new LightUpdateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLightUpdate;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLightUpdatePacket(this);

    //从光照引擎构造增量下发包对应原版构造函数
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
