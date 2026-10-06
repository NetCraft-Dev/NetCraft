using System.Collections.Generic;

namespace NetCraft.Network.Protocol.Configuration;

//ClientboundUpdateEnabledFeaturesPacket the server notifies which features are enabled
//Maps to vanilla net.minecraft.network.protocol.configuration.ClientboundUpdateEnabledFeaturesPacket
//Contains HashSet<Identifier> features, the set of enabled feature identifiers
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
