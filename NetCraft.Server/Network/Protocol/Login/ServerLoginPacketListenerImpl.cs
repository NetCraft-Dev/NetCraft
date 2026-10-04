using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Network.Protocol.Login;

//ServerLoginPacketListenerImpl 服务端 login 监听器实现
//简化版跳过加密收到 Hello 直接发 LoginFinished 等客户端 LoginAcknowledged 切换到 Configuration
//在线模式关闭时直接放行开启时暂不支持 Mojang 验证
public sealed class ServerLoginPacketListenerImpl : ServerLoginPacketListener
{
    private readonly Connection _connection;
    private readonly ServerLoginContext _context;
    private GameProfile? _profile;

    public ServerLoginPacketListenerImpl(Connection connection, ServerLoginContext context)
    {
        _connection = connection;
        _context = context;
    }

    //HandleHello 收到客户端 hello 存 GameProfile 后直接发 LoginFinished 跳过加密
    public void HandleHello(ServerboundHelloPacket packet)
    {
        Log.Debug($"HandleHello entry name={packet.Name} profileId={packet.ProfileId}");
        _profile = new GameProfile(packet.ProfileId, packet.Name);
        try
        {
            _connection.Send(new ClientboundLoginFinishedPacket(_profile, Guid.NewGuid()));
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to send LoginFinished {e.Message}");
            _connection.Disconnect("login failed");
        }
        //Log.Debug("HandleHello 出口");
    }

    //HandleKey 加密包本轮跳过加密不会调用空实现
    public void HandleKey(ServerboundKeyPacket packet)
    {
        Log.Debug("HandleKey entry, encryption skipped");
    }

    //HandleCustomQueryPacket 自定义查询包暂空实现
    public void HandleCustomQueryPacket(ServerboundCustomQueryAnswerPacket packet)
    {
        //Log.Debug("HandleCustomQueryPacket 入口");
    }

    //HandleLoginAcknowledgement 客户端确认登录完成切换到 Configuration 阶段
    public void HandleLoginAcknowledgement(ServerboundLoginAcknowledgedPacket packet)
    {
        //Log.Debug("HandleLoginAcknowledgement 入口");
        if (_profile is null)
        {
            Log.Warning("LoginAcknowledged received before Hello, disconnecting");
            _connection.Disconnect("login state error");
            return;
        }
        _context.TransitionToConfiguration(_connection, _profile);
        //Log.Debug("HandleLoginAcknowledgement 出口");
    }

    public void OnDisconnect(string reason)
    {
        Log.Info($"login phase disconnect reason={reason} profile={_profile?.Name ?? "none"}");
    }
}

//ServerLoginContext login 阶段上下文由 DedicatedServer 实现封装切到 Configuration 的逻辑
public interface ServerLoginContext
{
    //TransitionToConfiguration 切换连接到 Configuration 阶段挂 ServerConfigurationPacketListenerImpl
    //profile 已通过 Hello 验证的玩家档案
    void TransitionToConfiguration(Connection connection, GameProfile profile);
}
