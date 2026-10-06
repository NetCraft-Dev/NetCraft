using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Cookie;

//CookiePacketTypes cookie packet type registration, maps to vanilla net.minecraft.network.protocol.cookie.CookiePacketTypes
//Ids reuse the Play protocol space, for GameProtocols to bridge into Play with AddPacketCommon
public static class CookiePacketTypes
{
    //ClientboundCookieRequest asks the client to return the given cookie
    public static readonly PacketType<ClientCookiePacketListener> ClientboundCookieRequest =
        CreateClientbound<ClientCookiePacketListener>(21, "cookie_request");

    //ServerboundCookieResponse client returns a cookie
    public static readonly PacketType<ServerCookiePacketListener> ServerboundCookieResponse =
        CreateServerbound<ServerCookiePacketListener>(21, "cookie_response");

    //CreateClientbound registers a clientbound packet type with its id as the network ID
    private static PacketType<THandler> CreateClientbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(id, ConnectionProtocol.Play, FlowDirection.Clientbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));

    //CreateServerbound registers a serverbound packet type
    private static PacketType<THandler> CreateServerbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(id, ConnectionProtocol.Play, FlowDirection.Serverbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));
}
