namespace NetCraft.Network.Protocol.Configuration;

//ClientboundSelectKnownPacks 服务端询问客户端已加载的资源包
//对应原版 net.minecraft.network.protocol.configuration.ClientboundSelectKnownPacks
//含 List<KnownPack> 已知资源包列表客户端回传已加载的部分
public sealed record ClientboundSelectKnownPacks(List<KnownPack> KnownPacks) : Packet<ClientConfigurationPacketListener>
{
    public const int MaxPacks = 32;
    public const int MaxStringLength = 128;

    public static StreamCodec<FriendlyByteBuf, ClientboundSelectKnownPacks> StreamCodec { get; } = new SelectKnownPacksCodec();

    public PacketType<ClientConfigurationPacketListener> Type => ConfigurationPacketTypes.ClientboundSelectKnownPacks;

    public void Handle(ClientConfigurationPacketListener handler) => handler.HandleSelectKnownPacks(this);

    private sealed class SelectKnownPacksCodec : StreamCodec<FriendlyByteBuf, ClientboundSelectKnownPacks>
    {
        public ClientboundSelectKnownPacks Decode(FriendlyByteBuf buf)
        {
            var count = Math.Min(buf.ReadVarInt(), MaxPacks);
            var list = new List<KnownPack>(count);
            for (int i = 0; i < count; i++)
            {
                var ns = buf.ReadString(MaxStringLength);
                var id = buf.ReadString(MaxStringLength);
                var version = buf.ReadString(MaxStringLength);
                list.Add(new KnownPack(ns, id, version));
            }
            return new(list);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundSelectKnownPacks value)
        {
            buf.WriteVarInt(Math.Min(value.KnownPacks.Count, MaxPacks));
            foreach (var pack in value.KnownPacks)
            {
                buf.WriteString(pack.Namespace, MaxStringLength);
                buf.WriteString(pack.Id, MaxStringLength);
                buf.WriteString(pack.Version, MaxStringLength);
            }
        }
    }
}
