using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ClientboundCustomReportDetailsPacket 自定义报告详情包对应原版 net.minecraft.network.protocol.common.ClientboundCustomReportDetailsPacket
//含 Dictionary<string, string> 服务端发送给客户端用于崩溃报告附加信息
public sealed record ClientboundCustomReportDetailsPacket(Dictionary<string, string> Details) : Packet<ClientCommonPacketListener>
{
    public const int MaxEntries = 32;
    public const int MaxKeyLength = 128;
    public const int MaxValueLength = 4096;

    public static StreamCodec<FriendlyByteBuf, ClientboundCustomReportDetailsPacket> StreamCodec { get; } = new CustomReportDetailsCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundCustomReportDetails;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleCustomReportDetails(this);

    private sealed class CustomReportDetailsCodec : StreamCodec<FriendlyByteBuf, ClientboundCustomReportDetailsPacket>
    {
        public ClientboundCustomReportDetailsPacket Decode(FriendlyByteBuf buf)
        {
            var count = Math.Min(buf.ReadVarInt(), MaxEntries);
            var dict = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
                dict[buf.ReadString(MaxKeyLength)] = buf.ReadString(MaxValueLength);
            return new(dict);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundCustomReportDetailsPacket value)
        {
            buf.WriteVarInt(Math.Min(value.Details.Count, MaxEntries));
            foreach (var kv in value.Details)
            {
                buf.WriteString(kv.Key, MaxKeyLength);
                buf.WriteString(kv.Value, MaxValueLength);
            }
        }
    }
}
