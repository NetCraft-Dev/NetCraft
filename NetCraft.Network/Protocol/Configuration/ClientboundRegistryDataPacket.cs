using System.Collections.Generic;

namespace NetCraft.Network.Protocol.Configuration;

//ClientboundRegistryDataPacket the server sends registry data
//Maps to vanilla net.minecraft.network.protocol.configuration.ClientboundRegistryDataPacket
//Vanilla depends on ResourceKey and RegistrySynchronization.PackedRegistryEntry; simplified to
//  Identifier RegistryKey identifies which registry
//  byte[] Entries passes through the serialized bytes of the vanilla PackedRegistryEntry list (with a count prefix, no outer length)
//  EntriesCodec appends Entries directly to the stream, aligning with the vanilla ByteBufCodecs.list() layout
//  Switch back to strongly typed parsing once the RegistrySynchronization core is implemented
public sealed record ClientboundRegistryDataPacket(Identifier RegistryKey, byte[] Entries) : Packet<ClientConfigurationPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRegistryDataPacket> StreamCodec { get; } = new RegistryDataCodec();

    public PacketType<ClientConfigurationPacketListener> Type => ConfigurationPacketTypes.ClientboundRegistryData;

    public void Handle(ClientConfigurationPacketListener handler) => handler.HandleRegistryData(this);

    private sealed class RegistryDataCodec : StreamCodec<FriendlyByteBuf, ClientboundRegistryDataPacket>
    {
        //In vanilla entries is a list(count varint + PackedRegistryEntry...) with no outer byte length prefix
        //WriteBytes appends the PackBiomes bytes, already including the count prefix, as-is, avoiding an extra VarInt length layer that would misalign the client
        public ClientboundRegistryDataPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier(), buf.ReadBytes(buf.ReadableBytes));

        public void Encode(FriendlyByteBuf buf, ClientboundRegistryDataPacket value)
        {
            buf.WriteIdentifier(value.RegistryKey);
            buf.WriteBytes(value.Entries);
        }
    }
}
