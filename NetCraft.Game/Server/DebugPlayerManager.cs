using System.Security.Cryptography;
using System.Text;
using NetCraft.Game.Network;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Network.Protocol.Login;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//DebugPlayerManager 假玩家管理器 给 /debug join 提供指令驱动的"假客户端"
//假客户端用空流连接走真实 PlaceNewPlayer 流程 发出的包直接丢弃
//连接不进 DedicatedServer 连接表 不会被读循环判成流结束而断开
public sealed class DebugPlayerManager
{
    //WalkStepBlocks 逐步行走的每步位移 四分之一格接近原版行走节奏
    public const double WalkStepBlocks = 0.25;

    private readonly MinecraftServer _server;
    private readonly Dictionary<string, DebugPlayer> _players = new(StringComparer.OrdinalIgnoreCase);

    public DebugPlayerManager(MinecraftServer server) => _server = server;

    //Count 当前假玩家数量
    public int Count => _players.Count;

    //Join 创建假玩家并走真实加入世界流程 同名已存在返回 null
    public DebugPlayer? Join(string name)
    {
        if (_players.ContainsKey(name))
        {
            Log.Warning($"Debug player already exists name={name}");
            return null;
        }
        //离线模式 uuid 由名字派生 与真实离线客户端保持一致 同名玩家落到同一个档案
        var profile = new GameProfile(OfflineUuid(name), name);
        var connection = new Connection(Stream.Null, Stream.Null, PacketFlow.Serverbound);
        var listener = new ServerGamePacketListenerImpl(connection, profile);
        connection.SetListenerForServerboundGame(listener);
        var player = _server.PlayerList.PlaceNewPlayer(connection, profile);
        if (player is null)
        {
            connection.Disconnect("server-full");
            Log.Warning($"Debug player join failed, server is full name={name}");
            return null;
        }
        //依赖注入与真实连接一致 假玩家执行方块/命令操作时需要
        listener.Player = player;
        listener.Players = _server.PlayerList;
        listener.BlockEntities = _server.BlockEntities;
        listener.Commands = _server.Commands;
        //假玩家没有真实客户端回确认包 传送走直接下发分支 不设 Listener 免得卡在等待确认
        var fake = new DebugPlayer(player, connection, listener);
        _players[name] = fake;
        Log.Info($"Debug player joined name={name} entityId={player.EntityId} online {_server.PlayerList.PlayerCount}");
        return fake;
    }

    //Remove 移出假玩家并断开假连接 顺带广播退服提示
    public bool Remove(string name)
    {
        if (!_players.Remove(name, out var fake)) return false;
        DropTickets(fake.Player);
        _server.PlayerList.RemovePlayer(fake.Player);
        fake.Connection.Disconnect("debug-remove");
        Log.Info($"Debug player removed name={name}");
        return true;
    }

    //Find 按名字取假玩家
    public DebugPlayer? Find(string name) => _players.GetValueOrDefault(name);

    //Tick 推进所有假玩家的逐步行走 必须早于实体追踪这样本刻位移就能同步出去
    //顺带回收被外部断开的假连接(/kick 等命令) 假连接不在服务端连接表里没人替它清在线列表
    public void Tick()
    {
        foreach (var fake in _players.Values) fake.Tick();
        List<string>? dropped = null;
        foreach (var pair in _players)
        {
            if (pair.Value.Connection.IsConnected) continue;
            (dropped ??= new List<string>()).Add(pair.Key);
        }
        if (dropped is null) return;
        foreach (var name in dropped)
        {
            var fake = _players[name];
            _players.Remove(name);
            DropTickets(fake.Player);
            _server.PlayerList.RemovePlayer(fake.Player);
            Log.Info($"Debug player connection lost, removing from online list name={name}");
        }
    }

    //DropTickets 撤掉假玩家视距内出的票 与真实连接断开那条清理路径保持一致
    //不撤的话那片区块永远留在加载列表里: 票还在就意味着它一直是"该加载"的
    //普通卸载循环不敢碰它 之后用 debug 强制卸掉也会因为票仍在而每刻被重新拉起再卸掉
    private static void DropTickets(ServerPlayer player)
    {
        if (player.Level is PersistentServerLevel level)
            level.ChunkSource.RemovePlayerTickets(player);
    }

    //OfflineUuid 离线模式玩家 uuid 对齐原版 UUID.nameUUIDFromBytes("OfflinePlayer:" + name)
    private static Guid OfflineUuid(string name)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"OfflinePlayer:{name}"));
        //版本 3(MD5) 与变体位修正 再按大端还原 Java UUID 的 128 位
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash, bigEndian: true);
    }
}

//DebugPlayer 单个假玩家 持有真实加入流程产出的玩家对象与它的假连接
public sealed class DebugPlayer
{
    //_walkTarget 逐步行走终点 null 表示没在走
    private Vec3? _walkTarget;
    private int _walkInterval = 1;
    private int _walkCooldown;

    internal DebugPlayer(ServerPlayer player, Connection connection, ServerGamePacketListenerImpl listener)
    {
        Player = player;
        Connection = connection;
        Listener = listener;
    }

    public ServerPlayer Player { get; }
    public Connection Connection { get; }
    public ServerGamePacketListenerImpl Listener { get; }

    //IsWalking 是否正在逐步行走
    public bool IsWalking => _walkTarget is not null;

    //StartWalk 朝终点逐步行走 interval 为每步间隔刻数 1 表示每刻一步
    public void StartWalk(Vec3 target, int interval)
    {
        _walkTarget = target;
        _walkInterval = Math.Max(1, interval);
        _walkCooldown = 0;
    }

    //StopWalk 中断当前行走
    public void StopWalk() => _walkTarget = null;

    //Tick 每刻朝终点推进一步 到位即停
    //顺带替假客户端回心跳 真实客户端由 netty 自动回包 假连接不回会被服务端判超时踢掉
    public void Tick()
    {
        if (Player.PendingKeepAliveId is { } keepAliveId) Player.HandleKeepAliveResponse(keepAliveId);
        if (_walkTarget is not { } target) return;
        if (_walkCooldown > 0)
        {
            _walkCooldown--;
            return;
        }
        _walkCooldown = _walkInterval - 1;
        var current = Player.Position;
        var dx = target.X - current.X;
        var dy = target.Y - current.Y;
        var dz = target.Z - current.Z;
        var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (distance <= DebugPlayerManager.WalkStepBlocks)
        {
            Player.Position = target;
            _walkTarget = null;
            return;
        }
        var scale = DebugPlayerManager.WalkStepBlocks / distance;
        Player.Position = new Vec3(current.X + dx * scale, current.Y + dy * scale, current.Z + dz * scale);
        //朝向跟着前进方向 头身一起转 客户端模型靠移动包与头部旋转包刷新
        if (dx != 0 || dz != 0)
            Player.Yaw = (float)(Math.Atan2(-dx, dz) * (180.0 / Math.PI));
    }
}
