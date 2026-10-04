
namespace NetCraft.Network.Protocol.Common;

//ServerboundClientInformationPacket 客户端信息包对应原版 net.minecraft.network.protocol.common.ServerboundClientInformationPacket
//原版依赖 net.minecraft.server.level.ClientInformation 简化版直接内联字段
//字段顺序严格对齐 26.2：语言/视距(单字节)/聊天可见性/聊天颜色/modelCustomisation(单字节掩码)
//主手(枚举)/文本过滤/允许列表/粒子模式
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
        //原版 ClientInformation.write 顺序：
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

//ChatVisibility 聊天可见性枚举对应原版 net.minecraft.world.entity.player.ChatVisiblity
public enum ChatVisibility
{
    Full,
    System,
    Hidden
}

//HumanoidArm 主手枚举对应原版 net.minecraft.world.entity.HumanoidArm
//id 必须与枚举顺序一致 原版按 idMapper 写 VarInt id
public enum HumanoidArm
{
    Left,
    Right
}

//ParticleStatus 粒子状态枚举对应原版 net.minecraft.client.ParticleStatus
public enum ParticleStatus
{
    All,
    Decreased,
    Minimal
}
