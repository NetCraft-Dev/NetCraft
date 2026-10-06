namespace NetCraft.Network.Protocol.Configuration;

//ServerboundSelectKnownPacks the client returns the resource packs it has loaded
//Maps to vanilla net.minecraft.network.protocol.configuration.ServerboundSelectKnownPacks
//Contains List<KnownPack>, the list of resource packs the client has loaded
public sealed record ServerboundSelectKnownPacks(List<KnownPack> KnownPacks) : Packet<ServerConfigurationPacketListener>
{
    public const int MaxPacks = 32;
    public const int MaxStringLength = 128;

    public static StreamCodec<FriendlyByteBuf, ServerboundSelectKnownPacks> StreamCodec { get; } = new SelectKnownPacksCodec();

    public PacketType<ServerConfigurationPacketListener> Type => ConfigurationPacketTypes.ServerboundSelectKnownPacks;

    public void Handle(ServerConfigurationPacketListener handler) => handler.HandleSelectKnownPacks(this);

    private sealed class SelectKnownPacksCodec : StreamCodec<FriendlyByteBuf, ServerboundSelectKnownPacks>
    {
        public ServerboundSelectKnownPacks Decode(FriendlyByteBuf buf)
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

        public void Encode(FriendlyByteBuf buf, ServerboundSelectKnownPacks value)
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
