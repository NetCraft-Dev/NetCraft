namespace NetCraft.Network.Protocol.Login;

//ServerboundLoginAcknowledgedPacket 客户端登录确认包对应原版 net.minecraft.network.protocol.login.ServerboundLoginAcknowledgedPacket
//无 payload 单例模式 IsTerminal true 表示 LOGIN 阶段结束切换到 CONFIGURATION
public sealed record ServerboundLoginAcknowledgedPacket : Packet<ServerLoginPacketListener>
{
    //Instance 单例实例
    public static readonly ServerboundLoginAcknowledgedPacket Instance = new();

    //StreamCodec 恒定值编解码器
    public static StreamCodec<FriendlyByteBuf, ServerboundLoginAcknowledgedPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ServerboundLoginAcknowledgedPacket>(Instance);

    private ServerboundLoginAcknowledgedPacket() { }

    public PacketType<ServerLoginPacketListener> Type => LoginPacketTypes.ServerboundLoginAcknowledged;

    //IsTerminal 登录确认后切换到 CONFIGURATION
    public bool IsTerminal => true;

    public void Handle(ServerLoginPacketListener handler) => handler.HandleLoginAcknowledgement(this);
}
