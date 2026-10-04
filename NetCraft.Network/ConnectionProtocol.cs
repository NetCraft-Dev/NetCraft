namespace NetCraft.Network;

//ConnectionProtocol 协议枚举对应原版 net.minecraft.network.ConnectionProtocol
//Handshake 握手协议
//Play 游戏内协议
//Status 服务器列表协议
//Login 登录协议
//Configuration 配置协议
public enum ConnectionProtocol
{
    Handshake = 0,
    Play = 1,
    Status = 2,
    Login = 3,
    Configuration = 4
}

//ConnectionProtocolExtensions 协议枚举扩展方法
//提供 Id 字符串对齐原版 ConnectionProtocol.id()
public static class ConnectionProtocolExtensions
{
    //Id 返回协议小写名字符串对齐原版 id()
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
