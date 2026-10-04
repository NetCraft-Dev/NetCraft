using NetCraft.Network.Chat;

namespace NetCraft.Network.Protocol.Common;

//ClientboundDisconnectPacket 客户端断开连接包对应原版 net.minecraft.network.protocol.common.ClientboundDisconnectPacket
//reason 是组件按原版用组件流编解码 写成字符串会让客户端把长度前缀当 NBT tag id 解析失败
//IsTerminal true 表示断开后连接关闭
//Component 用全限定名 在外层命名空间 NetCraft.Network.* 下简单名会被 NetCraft.Network.Component 命名空间抢走
public sealed record ClientboundDisconnectPacket(NetCraft.Network.Chat.Component Reason) : Packet<ClientCommonPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDisconnectPacket> StreamCodec { get; } = new DisconnectCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundDisconnect;

    //IsTerminal 断开包后连接关闭
    public bool IsTerminal => true;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleDisconnect(this);

    private sealed class DisconnectCodec : StreamCodec<FriendlyByteBuf, ClientboundDisconnectPacket>
    {
        public ClientboundDisconnectPacket Decode(FriendlyByteBuf buf)
            => new(ComponentSerialization.StreamCodec.Decode(buf));

        public void Encode(FriendlyByteBuf buf, ClientboundDisconnectPacket value)
            => ComponentSerialization.StreamCodec.Encode(buf, value.Reason);
    }
}
