namespace NetCraft.Network.Protocol.Handshake;

//ClientIntent client intent, maps to vanilla net.minecraft.network.protocol.handshake.ClientIntent
//Status server list query
//Login login
//Transfer switches servers via transfer
public enum ClientIntent
{
    Status = 1,
    Login = 2,
    Transfer = 3
}

//ClientIntentExtensions extension methods for ClientIntent, aligns with vanilla byId/id
public static class ClientIntentExtensions
{
    //ById reverses the network ID to the enum value
    public static ClientIntent ById(int id) => id switch
    {
        1 => ClientIntent.Status,
        2 => ClientIntent.Login,
        3 => ClientIntent.Transfer,
        _ => throw new ArgumentException($"unknown client intent ID {id}"),
    };

    //Id returns the network ID
    public static int Id(this ClientIntent intent) => (int)intent;
}
