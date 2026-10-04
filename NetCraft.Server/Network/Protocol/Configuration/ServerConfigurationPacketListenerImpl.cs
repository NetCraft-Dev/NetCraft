using NetCraft.Network.Protocol.Cookie;
using NetCraft.Network.Protocol.Login;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Network.Protocol.Configuration;

//ServerConfigurationPacketListenerImpl 服务端 configuration 监听器实现
//核心处理 HandleConfigurationFinished 切换到 Play 阶段触发 PlayerList.PlaceNewPlayer
//对应原版任务队列 SynchronizeRegistries -> PrepareSpawn -> JoinWorld
//finish_configuration 等出生点预载完成后才发 对应原版 PrepareSpawnTask 完成后 JoinWorldTask 才放行
//其余 common/cookie 子协议包暂空实现因 ConfigurationProtocols 未注册这些包不会解码到
public sealed class ServerConfigurationPacketListenerImpl : ServerConfigurationPacketListener, TickablePacketListener
{
    private readonly Connection _connection;
    private readonly ServerConfigurationContext _context;
    private readonly GameProfile _profile;
    //出生点预载任务 对应原版 PrepareSpawnTask 的 chunkLoadFuture
    private Task[]? _spawnChunkTasks;

    public ServerConfigurationPacketListenerImpl(Connection connection, GameProfile profile, ServerConfigurationContext context)
    {
        _connection = connection;
        _profile = profile;
        _context = context;
    }

    //HandleConfigurationFinished 客户端完成配置切换到 Play 阶段挂 ServerGamePacketListenerImpl
    public void HandleConfigurationFinished(ServerboundFinishConfigurationPacket packet)
    {
        Log.Debug($"HandleConfigurationFinished entry profile={_profile.Name}");
        _context.TransitionToGame(_connection, _profile);
        //Log.Debug("HandleConfigurationFinished 出口");
    }

    //HandleSelectKnownPacks 客户端回复选中的 known pack 后发送 registry_data 并启动出生点预载
    //对齐原版 SynchronizeRegistriesTask.handleResponse 时机 empty contents 依赖客户端此时已初始化本地资源
    //finish_configuration 挪到 TickListener 出生点区块就绪后发 避免玩家进世界看到未加载的虚空
    public void HandleSelectKnownPacks(ServerboundSelectKnownPacks packet)
    {
        Log.Debug($"HandleSelectKnownPacks entry packs={packet.KnownPacks.Count}");
        try
        {
            _context.SendSynchronizedRegistries(_connection);
            _spawnChunkTasks = _context.PrepareSpawnChunks();
            Log.Debug($"HandleSelectKnownPacks sent registry_data and submitted spawn preload for {_spawnChunkTasks.Length} chunks");
        }
        catch (Exception e)
        {
            Log.Warning($"registry_data send failed {_profile.Name} {e.Message}");
        }
    }

    //TickListener 每 tick 轮询出生点预载任务全部完成后发 finish_configuration
    //对应原版 PrepareSpawnTask.tick 的 chunkLoadFuture 等待与 JoinWorldTask 的放行
    //加载失败也放行 由 play 阶段 ChunkSender 按 IsChunkFailed 丢弃
    public void TickListener()
    {
        if (_spawnChunkTasks is null) return;
        //票只有 20 tick 超时 加载没完就得一直续 对应原版 PrepareSpawnTask.Ready.keepAlive
        //不续的话票一过期区块就被回收 玩家进世界看到的还是空洞
        _context.KeepAliveSpawnTickets();
        foreach (var task in _spawnChunkTasks)
            if (!task.IsCompleted) return;
        _connection.Send(ClientboundFinishConfigurationPacket.Instance);
        _spawnChunkTasks = null;
        Log.Debug("Spawn preload finished, finish_configuration sent");
    }

    //HandleAcceptCodeOfConduct 行为准则接受空实现
    public void HandleAcceptCodeOfConduct(ServerboundAcceptCodeOfConductPacket packet)
    {
        //Log.Debug("HandleAcceptCodeOfConduct 入口");
    }

    //以下为继承自 ServerCommonPacketListener 的方法
    //ConfigurationProtocols 未注册这些包不会解码到暂空实现
    public void HandleClientInformation(ServerboundClientInformationPacket packet) { }
    public void HandleCustomPayload(ServerboundCustomPayloadPacket packet) { }
    public void HandleKeepAlive(ServerboundKeepAlivePacket packet) { }
    public void HandlePong(ServerboundPongPacket packet) { }
    public void HandleResourcePack(ServerboundResourcePackPacket packet) { }
    public void HandleCustomClickAction(ServerboundCustomClickActionPacket packet) { }

    //继承自 ServerCookiePacketListener
    public void HandleCookieResponse(ServerboundCookieResponsePacket packet) { }

    public void OnDisconnect(string reason)
    {
        Log.Info($"configuration phase disconnect reason={reason} profile={_profile.Name}");
    }
}

//ServerConfigurationContext configuration 阶段上下文由 DedicatedServer 实现封装切到 Game 的逻辑
public interface ServerConfigurationContext
{
    //TransitionToGame 切换连接到 Play 阶段挂 ServerGamePacketListenerImpl 并触发 PlayerList.PlaceNewPlayer
    void TransitionToGame(Connection connection, GameProfile profile);

    //SendSynchronizedRegistries 发送全部 SYNCHRONIZED_REGISTRIES 的 registry_data
    void SendSynchronizedRegistries(Connection connection);

    //PrepareSpawnChunks 提交出生点周围区块加载并返回任务列表 对应原版 PrepareSpawnTask 的 PLAYER_SPAWN ticket
    Task[] PrepareSpawnChunks();

    //KeepAliveSpawnTickets 续出生点预载票 对应原版 PrepareSpawnTask.Ready.keepAlive
    void KeepAliveSpawnTickets();
}
