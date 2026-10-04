namespace NetCraft.Network;

//PacketListener 包监听器接口对应原版 net.minecraft.network.PacketListener
//所有协议处理器实现此接口接收对应协议包
//THandler 自引用约束子类必须是自己避免误用
public interface PacketListener
{
    //Flow 包方向 SERVERBOUND 或 CLIENTBOUND
    FlowDirection Flow { get; }

    //Protocol 当前协议状态
    ConnectionProtocol Protocol { get; }

    //OnDisconnect 断开连接时调用
    void OnDisconnect(string reason);

    //IsAcceptingMessages 是否接收新包默认 true
    bool IsAcceptingMessages => true;

    //ShouldHandleMessage 是否处理该包默认按 IsAcceptingMessages
    bool ShouldHandleMessage<THandler>(Packet<THandler> packet) where THandler : class
        => IsAcceptingMessages;
}

//ServerboundPacketListener 服务端收包监听器对应原版 net.minecraft.network.ServerboundPacketListener
//Flow 固定 SERVERBOUND
public interface ServerboundPacketListener : PacketListener
{
    FlowDirection PacketListener.Flow => FlowDirection.Serverbound;
}

//ClientboundPacketListener 客户端收包监听器对应原版 net.minecraft.network.ClientboundPacketListener
//Flow 固定 CLIENTBOUND
public interface ClientboundPacketListener : PacketListener
{
    FlowDirection PacketListener.Flow => FlowDirection.Clientbound;
}

//DisconnectionDetails 断开连接详情对应原版 net.minecraft.network.DisconnectionDetails
//简化版只含 reason 字符串
public sealed record DisconnectionDetails(string Reason);

//TickablePacketListener 可 tick 监听器对应原版 net.minecraft.network.TickablePacketListener
//Connection.Tick 每 tick 调用一次供监听器轮询异步任务
public interface TickablePacketListener : PacketListener
{
    //TickListener 每 tick 调用 对应原版 tick()
    void TickListener();
}
