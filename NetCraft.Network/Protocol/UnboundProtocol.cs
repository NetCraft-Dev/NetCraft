namespace NetCraft.Network.Protocol;

//UnboundProtocol 未绑定上下文的协议对应原版 net.minecraft.network.protocol.UnboundProtocol
//提供 bind 方法接受上下文包装器和上下文 C 返回绑定的 ProtocolInfo
//C 是上下文类型如 Configuration 上下文含注册表访问
public interface UnboundProtocol<THandler, C> : ProtocolInfo<THandler>.DetailsProvider
{
    //Bind 用上下文包装器和上下文绑定返回 ProtocolInfo
    ProtocolInfo<THandler> Bind(C context);
}

//SimpleUnboundProtocol 无上下文协议对应原版 net.minecraft.network.protocol.SimpleUnboundProtocol
//C = Unit 简化形式 bind 不需要额外上下文
public interface SimpleUnboundProtocol<THandler> : ProtocolInfo<THandler>.DetailsProvider
{
    //Bind 绑定返回 ProtocolInfo
    ProtocolInfo<THandler> Bind();
}
