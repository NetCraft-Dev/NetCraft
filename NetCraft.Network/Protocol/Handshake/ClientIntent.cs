namespace NetCraft.Network.Protocol.Handshake;

//ClientIntent 客户端意图对应原版 net.minecraft.network.protocol.handshake.ClientIntent
//Status 服务器列表查询
//Login 登录
//Transfer 通过转传方式切换服务器
public enum ClientIntent
{
    Status = 1,
    Login = 2,
    Transfer = 3
}

//ClientIntentExtensions ClientIntent 扩展方法对齐原版 byId/id
public static class ClientIntentExtensions
{
    //ById 按网络 ID 反查枚举值
    public static ClientIntent ById(int id) => id switch
    {
        1 => ClientIntent.Status,
        2 => ClientIntent.Login,
        3 => ClientIntent.Transfer,
        _ => throw new ArgumentException($"未知客户端意图 ID {id}"),
    };

    //Id 返回网络 ID
    public static int Id(this ClientIntent intent) => (int)intent;
}
