using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ServerLink 服务器链接条目对应原版 net.minecraft.server.ServerLinks.Entry
//Type 是已知类型 id 可空表示自定义链接 Label 是显示文本原版用 Component 简化为 string
public sealed record ServerLink(Identifier? Type, string Label, string Url);

//ClientboundServerLinksPacket 服务器链接包对应原版 net.minecraft.network.protocol.common.ClientboundServerLinksPacket
//含 List<ServerLink> 服务端发送的自定义链接
public sealed record ClientboundServerLinksPacket(List<ServerLink> Links) : Packet<ClientCommonPacketListener>
{
    public const int MaxLinks = 32;
    public const int MaxUrlLength = 1024;
    public const int MaxLabelLength = 262144;

    public static StreamCodec<FriendlyByteBuf, ClientboundServerLinksPacket> StreamCodec { get; } = new ServerLinksCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundServerLinks;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleServerLinks(this);

    private sealed class ServerLinksCodec : StreamCodec<FriendlyByteBuf, ClientboundServerLinksPacket>
    {
        public ClientboundServerLinksPacket Decode(FriendlyByteBuf buf)
        {
            var count = Math.Min(buf.ReadVarInt(), MaxLinks);
            var list = new List<ServerLink>(count);
            for (int i = 0; i < count; i++)
            {
                var hasType = buf.ReadBoolean();
                Identifier? type = hasType ? buf.ReadIdentifier() : null;
                var label = buf.ReadString(MaxLabelLength);
                var url = buf.ReadString(MaxUrlLength);
                list.Add(new(type, label, url));
            }
            return new(list);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundServerLinksPacket value)
        {
            buf.WriteVarInt(Math.Min(value.Links.Count, MaxLinks));
            foreach (var link in value.Links)
            {
                buf.WriteBoolean(link.Type is not null);
                if (link.Type is not null)
                    buf.WriteIdentifier(link.Type.Value);
                buf.WriteString(link.Label, MaxLabelLength);
                buf.WriteString(link.Url, MaxUrlLength);
            }
        }
    }
}
