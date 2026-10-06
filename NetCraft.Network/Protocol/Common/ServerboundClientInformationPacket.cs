
namespace NetCraft.Network.Protocol.Common;

//ServerboundClientInformationPacket client information packet, maps to vanilla net.minecraft.network.protocol.common.ServerboundClientInformationPacket
//Vanilla depends on net.minecraft.server.level.ClientInformation; the simplified form inlines the fields
//Field order strictly aligns with 26.2: language/view distance(single byte)/chat visibility/chat colors/modelCustomisation(single byte mask)
//main hand(enum)/text filtering/allow listing/particle status
public sealed record ServerboundClientInformationPacket(
    string Language,
    int ViewDistance,
    ChatVisibility ChatVisibility,
    bool ChatColors,
    int ModelCustomisation,
    HumanoidArm MainHand,
    bool TextFiltering,
    bool AllowListing,
    ParticleStatus ParticleStatus) : Packet<ServerCommonPacketListener>
{
    public const int MaxLanguageLength = 16;
    public const int MaxViewDistance = 32;

    public static StreamCodec<FriendlyByteBuf, ServerboundClientInformationPacket> StreamCodec { get; } = new ClientInfoCodec();

    public PacketType<ServerCommonPacketListener> Type => CommonPacketTypes.ServerboundClientInformation;

    public void Handle(ServerCommonPacketListener handler) => handler.HandleClientInformation(this);

    private sealed class ClientInfoCodec : StreamCodec<FriendlyByteBuf, ServerboundClientInformationPacket>
    {
        //Vanilla ClientInformation.write order:
        //writeUtf(language) writeByte(viewDistance) writeEnum(chatVisibility) writeBoolean(chatColors)
        //writeByte(modelCustomisation) writeEnum(mainHand) writeBoolean(textFiltering) writeBoolean(allowsListing) writeEnum(particleStatus)
        public ServerboundClientInformationPacket Decode(FriendlyByteBuf buf)
            => new(
                buf.ReadString(MaxLanguageLength),
                Math.Clamp((int)buf.ReadByte(), 0, MaxViewDistance),
                (ChatVisibility)buf.ReadVarInt(),
                buf.ReadBoolean(),
                buf.ReadByte(),
                (HumanoidArm)buf.ReadVarInt(),
                buf.ReadBoolean(),
                buf.ReadBoolean(),
                (ParticleStatus)buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundClientInformationPacket value)
        {
            buf.WriteString(value.Language, MaxLanguageLength);
            buf.WriteByte((byte)value.ViewDistance);
            buf.WriteVarInt((int)value.ChatVisibility);
            buf.WriteBoolean(value.ChatColors);
            buf.WriteByte((byte)value.ModelCustomisation);
            buf.WriteVarInt((int)value.MainHand);
            buf.WriteBoolean(value.TextFiltering);
            buf.WriteBoolean(value.AllowListing);
            buf.WriteVarInt((int)value.ParticleStatus);
        }
    }
}

//ChatVisibility chat visibility enum, maps to vanilla net.minecraft.world.entity.player.ChatVisiblity
public enum ChatVisibility
{
    Full,
    System,
    Hidden
}

//HumanoidArm main hand enum, maps to vanilla net.minecraft.world.entity.HumanoidArm
//id must match the enum order; vanilla writes the VarInt id via idMapper
public enum HumanoidArm
{
    Left,
    Right
}

//ParticleStatus particle status enum, maps to vanilla net.minecraft.client.ParticleStatus
public enum ParticleStatus
{
    All,
    Decreased,
    Minimal
}
