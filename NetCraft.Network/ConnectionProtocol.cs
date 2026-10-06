namespace NetCraft.Network;

//ConnectionProtocol protocol enum, maps to vanilla net.minecraft.network.ConnectionProtocol
//Handshake handshake protocol
//Play in-game protocol
//Status server list protocol
//Login login protocol
//Configuration configuration protocol
public enum ConnectionProtocol
{
    Handshake = 0,
    Play = 1,
    Status = 2,
    Login = 3,
    Configuration = 4
}

//ConnectionProtocolExtensions extension methods for the protocol enum
//Provides the Id string, aligns with vanilla ConnectionProtocol.id()
public static class ConnectionProtocolExtensions
{
    //Id returns the lowercase protocol name string, aligns with vanilla id()
    public static string Id(this ConnectionProtocol protocol) => protocol switch
    {
        ConnectionProtocol.Handshake => "handshake",
        ConnectionProtocol.Play => "play",
        ConnectionProtocol.Status => "status",
        ConnectionProtocol.Login => "login",
        ConnectionProtocol.Configuration => "configuration",
        _ => protocol.ToString().ToLowerInvariant(),
    };
}
