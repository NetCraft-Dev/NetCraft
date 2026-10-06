namespace NetCraft.Network.Protocol.Common;

//ClientboundUpdateTagsPacket update tags packet, maps to vanilla net.minecraft.network.protocol.common.ClientboundUpdateTagsPacket
//Two-level map: registry Identifier → (tag name → array of that registry's int ids), maps to TagNetworkSerialization.NetworkPayload
//The int id is the registry entry index (matching the registry_data network order); the client looks the Holder back up by index
public sealed record ClientboundUpdateTagsPacket(
    Dictionary<NetCraft.Registry.Identifier, Dictionary<NetCraft.Registry.Identifier, int[]>> Tags)
    : Packet<ClientCommonPacketListener>
{
    public const int MaxRegistries = 32767;
    public const int MaxTagsPerRegistry = 32767;

    public static StreamCodec<FriendlyByteBuf, ClientboundUpdateTagsPacket> StreamCodec { get; } = new UpdateTagsCodec();

    //Type packet type identity, used only as an identity and no longer determines the network ID
    //This packet is registered in both Configuration and Play (13/134); on encode the current protocol table looks it up by packet class
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
