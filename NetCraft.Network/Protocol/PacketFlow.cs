namespace NetCraft.Network.Protocol;

//PacketFlow 包方向枚举对应原版 net.minecraft.network.protocol.PacketFlow
//Serverbound 客户端发往服务端
//Clientbound 服务端发往客户端
public enum PacketFlow
{
    Serverbound,
    Clientbound
}

//PacketFlowExtensions 包方向扩展方法对齐原版 PacketFlow.id/getOpposite
public static class PacketFlowExtensions
{
    //Id 返回方向小写名字符串
    public static string Id(this PacketFlow flow) => flow switch
    {
        PacketFlow.Serverbound => "serverbound",
        PacketFlow.Clientbound => "clientbound",
        _ => flow.ToString().ToLowerInvariant(),
    };

    //GetOpposite 返回反方向
    public static PacketFlow GetOpposite(this PacketFlow flow) =>
        flow == PacketFlow.Clientbound ? PacketFlow.Serverbound : PacketFlow.Clientbound;
}
