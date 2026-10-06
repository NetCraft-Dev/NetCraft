using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Login;

//LoginPacketTypes login packet type registration, maps to vanilla net.minecraft.network.protocol.login.LoginPacketTypes
//5 clientbound + 4 serverbound packet type IDs, aligning with the vanilla registration order
//clientbound: login_disconnect=0 hello=1 login_finished=2 login_compression=3 custom_query=4
//serverbound: hello=0 key=1 custom_query_answer=2 login_acknowledged=3
public static class LoginPacketTypes
{
    //ClientboundLoginDisconnect login disconnect packet minecraft:login_disconnect
    public static readonly PacketType<ClientLoginPacketListener> ClientboundLoginDisconnect =
        CreateClientbound<ClientLoginPacketListener>(0, "login_disconnect");

    //ClientboundHello encryption handshake packet minecraft:hello
    public static readonly PacketType<ClientLoginPacketListener> ClientboundHello =
        CreateClientbound<ClientLoginPacketListener>(1, "hello");

    //ClientboundLoginFinished login finished packet minecraft:login_finished
    public static readonly PacketType<ClientLoginPacketListener> ClientboundLoginFinished =
        CreateClientbound<ClientLoginPacketListener>(2, "login_finished");

    //ClientboundLoginCompression compression notification packet minecraft:login_compression
    public static readonly PacketType<ClientLoginPacketListener> ClientboundLoginCompression =
        CreateClientbound<ClientLoginPacketListener>(3, "login_compression");

    //ClientboundCustomQuery custom query request packet minecraft:custom_query
    public static readonly PacketType<ClientLoginPacketListener> ClientboundCustomQuery =
        CreateClientbound<ClientLoginPacketListener>(4, "custom_query");

    //ServerboundHello login hello packet minecraft:hello
    public static readonly PacketType<ServerLoginPacketListener> ServerboundHello =
        CreateServerbound<ServerLoginPacketListener>(0, "hello");

    //ServerboundKey encryption key packet minecraft:key
    public static readonly PacketType<ServerLoginPacketListener> ServerboundKey =
        CreateServerbound<ServerLoginPacketListener>(1, "key");

    //ServerboundCustomQueryAnswer custom query answer packet minecraft:custom_query_answer
    public static readonly PacketType<ServerLoginPacketListener> ServerboundCustomQueryAnswer =
        CreateServerbound<ServerLoginPacketListener>(2, "custom_query_answer");

    //ServerboundLoginAcknowledged login acknowledged packet minecraft:login_acknowledged
    public static readonly PacketType<ServerLoginPacketListener> ServerboundLoginAcknowledged =
        CreateServerbound<ServerLoginPacketListener>(3, "login_acknowledged");

    //CreateClientbound registers a clientbound packet type with its id as the network ID
    private static PacketType<THandler> CreateClientbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(
            id, ConnectionProtocol.Login, FlowDirection.Clientbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));

    //CreateServerbound registers a serverbound packet type
    private static PacketType<THandler> CreateServerbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(
            id, ConnectionProtocol.Login, FlowDirection.Serverbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));
}
