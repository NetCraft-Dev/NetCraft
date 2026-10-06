namespace NetCraft.Network.Protocol.Configuration;

//ClientboundCodeOfConductPacket server sends the code of conduct
//Maps to vanilla net.minecraft.network.protocol.configuration.ClientboundCodeOfConductPacket
//Contains string codeOfConduct, the code of conduct text
public sealed record ClientboundCodeOfConductPacket(string CodeOfConduct) : Packet<ClientConfigurationPacketListener>
{
    public const int MaxCodeOfConductLength = 32767;

    public static StreamCodec<FriendlyByteBuf, ClientboundCodeOfConductPacket> StreamCodec { get; } = new CodeOfConductCodec();

    public PacketType<ClientConfigurationPacketListener> Type => ConfigurationPacketTypes.ClientboundCodeOfConduct;

    public void Handle(ClientConfigurationPacketListener handler) => handler.HandleCodeOfConduct(this);

    private sealed class CodeOfConductCodec : StreamCodec<FriendlyByteBuf, ClientboundCodeOfConductPacket>
    {
        public ClientboundCodeOfConductPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadString(MaxCodeOfConductLength));

        public void Encode(FriendlyByteBuf buf, ClientboundCodeOfConductPacket value)
            => buf.WriteString(value.CodeOfConduct, MaxCodeOfConductLength);
    }
}
