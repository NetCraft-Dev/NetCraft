namespace NetCraft.Network.Protocol;

//UnboundProtocol protocol without a bound context, maps to vanilla net.minecraft.network.protocol.UnboundProtocol
//Provides a bind method taking a context wrapper and context C, returning a bound ProtocolInfo
//C is the context type, such as the Configuration context containing registry access
public interface UnboundProtocol<THandler, C> : ProtocolInfo<THandler>.DetailsProvider
{
    //Bind binds with a context wrapper and context and returns a ProtocolInfo
    ProtocolInfo<THandler> Bind(C context);
}

//SimpleUnboundProtocol context-free protocol, maps to vanilla net.minecraft.network.protocol.SimpleUnboundProtocol
//C = Unit simplified form where bind needs no extra context
public interface SimpleUnboundProtocol<THandler> : ProtocolInfo<THandler>.DetailsProvider
{
    //Bind binds and returns a ProtocolInfo
    ProtocolInfo<THandler> Bind();
}
