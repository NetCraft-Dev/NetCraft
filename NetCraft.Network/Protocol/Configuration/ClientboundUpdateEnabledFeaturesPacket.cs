using System.Collections.Generic;

namespace NetCraft.Network.Protocol.Configuration;

//ClientboundUpdateEnabledFeaturesPacket 服务端通知启用的特性
//对应原版 net.minecraft.network.protocol.configuration.ClientboundUpdateEnabledFeaturesPacket
//含 HashSet<Identifier> features 启用的特性标识符集合
public sealed record ClientboundUpdateEnabledFeaturesPacket(HashSet<Identifier> Features) : Packet<ClientConfigurationPacketListener>
{
    public const int MaxFeatures = 1024;

    public static StreamCodec<FriendlyByteBuf, ClientboundUpdateEnabledFeaturesPacket> StreamCodec { get; } = new FeaturesCodec();

    public PacketType<ClientConfigurationPacketListener> Type => ConfigurationPacketTypes.ClientboundUpdateEnabledFeatures;

    public void Handle(ClientConfigurationPacketListener handler) => handler.HandleEnabledFeatures(this);

    private sealed class FeaturesCodec : StreamCodec<FriendlyByteBuf, ClientboundUpdateEnabledFeaturesPacket>
    {
        public ClientboundUpdateEnabledFeaturesPacket Decode(FriendlyByteBuf buf)
        {
            var count = Math.Min(buf.ReadVarInt(), MaxFeatures);
            var set = new HashSet<Identifier>();
            for (int i = 0; i < count; i++)
                set.Add(buf.ReadIdentifier());
            return new(set);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundUpdateEnabledFeaturesPacket value)
        {
            buf.WriteVarInt(Math.Min(value.Features.Count, MaxFeatures));
            foreach (var id in value.Features)
                buf.WriteIdentifier(id);
        }
    }
}
