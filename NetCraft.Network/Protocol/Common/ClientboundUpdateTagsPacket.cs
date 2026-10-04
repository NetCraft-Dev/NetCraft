namespace NetCraft.Network.Protocol.Common;

//ClientboundUpdateTagsPacket 更新标签包对应原版 net.minecraft.network.protocol.common.ClientboundUpdateTagsPacket
//两层 map: 注册表 Identifier → (tag 名 → 该注册表 int id 数组) 对应 TagNetworkSerialization.NetworkPayload
//int id 是注册表条目序号(与 registry_data 网络顺序一致)客户端按序号查回 Holder
public sealed record ClientboundUpdateTagsPacket(
    Dictionary<NetCraft.Registry.Identifier, Dictionary<NetCraft.Registry.Identifier, int[]>> Tags)
    : Packet<ClientCommonPacketListener>
{
    public const int MaxRegistries = 32767;
    public const int MaxTagsPerRegistry = 32767;

    public static StreamCodec<FriendlyByteBuf, ClientboundUpdateTagsPacket> StreamCodec { get; } = new UpdateTagsCodec();

    //Type 包类型标识 只作标识不再决定网络 ID
    //本包在 Configuration 与 Play 都注册(13/134) 编码时由当前协议表按包类反查
    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundUpdateTags;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleUpdateTags(this);

    private sealed class UpdateTagsCodec : StreamCodec<FriendlyByteBuf, ClientboundUpdateTagsPacket>
    {
        public ClientboundUpdateTagsPacket Decode(FriendlyByteBuf buf)
        {
            var count = buf.ReadVarInt();
            var tags = new Dictionary<NetCraft.Registry.Identifier, Dictionary<NetCraft.Registry.Identifier, int[]>>(Math.Min(count, MaxRegistries));
            for (int i = 0; i < count; i++)
            {
                var registry = buf.ReadIdentifier();
                var tagCount = Math.Min(buf.ReadVarInt(), MaxTagsPerRegistry);
                var payload = new Dictionary<NetCraft.Registry.Identifier, int[]>(tagCount);
                for (int j = 0; j < tagCount; j++)
                {
                    var tagName = buf.ReadIdentifier();
                    var len = buf.ReadVarInt();
                    var ids = new int[len];
                    for (int k = 0; k < len; k++)
                        ids[k] = buf.ReadVarInt();
                    payload[tagName] = ids;
                }
                tags[registry] = payload;
            }
            return new(tags);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundUpdateTagsPacket value)
        {
            buf.WriteVarInt(value.Tags.Count);
            foreach (var registry in value.Tags)
            {
                buf.WriteIdentifier(registry.Key);
                buf.WriteVarInt(registry.Value.Count);
                foreach (var tag in registry.Value)
                {
                    buf.WriteIdentifier(tag.Key);
                    buf.WriteVarInt(tag.Value.Length);
                    foreach (var id in tag.Value)
                        buf.WriteVarInt(id);
                }
            }
        }
    }
}
