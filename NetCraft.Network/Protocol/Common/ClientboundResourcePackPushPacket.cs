
namespace NetCraft.Network.Protocol.Common;

//ClientboundResourcePackPushPacket 资源包推送包对应原版 net.minecraft.network.protocol.common.ClientboundResourcePackPushPacket
//含 UUID id + url + hash + required + forced + prompt(string 简化版)
//原版用 Component prompt 简化为 string
public sealed record ClientboundResourcePackPushPacket(
    Guid Id,
    string Url,
    string Hash,
    bool Required,
    bool Forced,
    string Prompt) : Packet<ClientCommonPacketListener>
{
    public const int MaxUrlLength = 4096;
    public const int MaxHashLength = 64;
    public const int MaxPromptLength = 262144;

    public static StreamCodec<FriendlyByteBuf, ClientboundResourcePackPushPacket> StreamCodec { get; } = new ResourcePackPushCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundResourcePackPush;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleResourcePackPush(this);

    private sealed class ResourcePackPushCodec : StreamCodec<FriendlyByteBuf, ClientboundResourcePackPushPacket>
    {
        public ClientboundResourcePackPushPacket Decode(FriendlyByteBuf buf)
            => new(
                buf.ReadUuid(),
                buf.ReadString(MaxUrlLength),
                buf.ReadString(MaxHashLength),
                buf.ReadBoolean(),
                buf.ReadBoolean(),
                buf.ReadString(MaxPromptLength));

        public void Encode(FriendlyByteBuf buf, ClientboundResourcePackPushPacket value)
        {
            buf.WriteUuid(value.Id);
            buf.WriteString(value.Url, MaxUrlLength);
            buf.WriteString(value.Hash, MaxHashLength);
            buf.WriteBoolean(value.Required);
            buf.WriteBoolean(value.Forced);
            buf.WriteString(value.Prompt, MaxPromptLength);
        }
    }
}
