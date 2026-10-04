using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Cookie;

//CookiePacketTypes cookie 包类型注册 对应原版 net.minecraft.network.protocol.cookie.CookiePacketTypes
//id 沿用 Play 协议空间 供 GameProtocols 用 AddPacketCommon 桥接进 Play
public static class CookiePacketTypes
{
    //ClientboundCookieRequest 请求客户端回传指定 cookie
    public static readonly PacketType<ClientCookiePacketListener> ClientboundCookieRequest =
        CreateClientbound<ClientCookiePacketListener>(21, "cookie_request");

    //ServerboundCookieResponse 客户端回传 cookie
    public static readonly PacketType<ServerCookiePacketListener> ServerboundCookieResponse =
        CreateServerbound<ServerCookiePacketListener>(21, "cookie_response");

    //CreateClientbound 注册 clientbound 包类型 id 为网络 ID
    private static PacketType<THandler> CreateClientbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(id, ConnectionProtocol.Play, FlowDirection.Clientbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));

    //CreateServerbound 注册 serverbound 包类型
    private static PacketType<THandler> CreateServerbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(id, ConnectionProtocol.Play, FlowDirection.Serverbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));
}
