namespace NetCraft.Network.Protocol;

//PacketFlow packet direction enum, maps to vanilla net.minecraft.network.protocol.PacketFlow
//Serverbound client to server
//Clientbound server to client
public enum PacketFlow
{
    Serverbound,
    Clientbound
}

//PacketFlowExtensions extension methods for packet direction, aligns with vanilla PacketFlow.id/getOpposite
public static class PacketFlowExtensions
{
    //Id returns the lowercase direction name string
    public static string Id(this PacketFlow flow) => flow switch
    {
        PacketFlow.Serverbound => "serverbound",
        PacketFlow.Clientbound => "clientbound",
        _ => flow.ToString().ToLowerInvariant(),
    };

    //GetOpposite returns the opposite direction
    public static PacketFlow GetOpposite(this PacketFlow flow) =>
        flow == PacketFlow.Clientbound ? PacketFlow.Serverbound : PacketFlow.Clientbound;
}
