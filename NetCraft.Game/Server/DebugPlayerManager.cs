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

//DebugPlayerManager fake player manager, providing a command-driven "fake client" for /debug join
//The fake client uses an empty stream connection through the real PlaceNewPlayer flow; packets it sends are discarded
//The connection is not in the DedicatedServer connection table, so the read loop does not treat it as end-of-stream and disconnect it
public sealed class DebugPlayerManager
{
    //WalkStepBlocks the per-step displacement of stepping walking; a quarter block matches the vanilla walking pace
    public const double WalkStepBlocks = 0.25;

    private readonly MinecraftServer _server;
    private readonly Dictionary<string, DebugPlayer> _players = new(StringComparer.OrdinalIgnoreCase);

    public DebugPlayerManager(MinecraftServer server) => _server = server;

    //Count current fake player count
    public int Count => _players.Count;

    //Join creates a fake player going through the real join-world flow; returns null when the name already exists
    public DebugPlayer? Join(string name)
    {
        if (_players.ContainsKey(name))
        {
            Log.Warning($"Debug player already exists name={name}");
            return null;
        }
        //In offline mode the uuid is derived from the name, consistent with a real offline client, so the same-name player lands in the same profile
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
        //Dependency injection matches a real connection, needed when the fake player performs block/command operations
        listener.Player = player;
        listener.Players = _server.PlayerList;
        listener.BlockEntities = _server.BlockEntities;
        listener.Commands = _server.Commands;
        //A fake player has no real client to return an ack packet, so teleports go through the direct-send branch; no Listener is set to avoid waiting for an ack
        var fake = new DebugPlayer(player, connection, listener);
        _players[name] = fake;
        Log.Info($"Debug player joined name={name} entityId={player.EntityId} online {_server.PlayerList.PlayerCount}");
        return fake;
    }

    //Remove removes the fake player and disconnects the fake connection, also broadcasting the leave message
    public bool Remove(string name)
    {
        if (!_players.Remove(name, out var fake)) return false;
        DropTickets(fake.Player);
        _server.PlayerList.RemovePlayer(fake.Player);
        fake.Connection.Disconnect("debug-remove");
        Log.Info($"Debug player removed name={name}");
        return true;
    }

    //Find gets a fake player by name
    public DebugPlayer? Find(string name) => _players.GetValueOrDefault(name);

    //Tick advances all fake players' stepping walking; it must precede entity tracking so this tick's movement can be synced out
    //Also reclaims fake connections disconnected externally (by commands such as /kick); the fake connection is not in the server connection table so nothing else clears its online entry
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

    //DropTickets drops the tickets within the fake player's view distance, consistent with the cleanup path of a real disconnect
    //Without dropping them that chunk stays in the load list forever: a ticket still present means it is always "should load"
    //The normal unload loop does not touch it, and a later debug force unload is pulled back up every tick because the ticket is still there
    private static void DropTickets(ServerPlayer player)
    {
        if (player.Level is PersistentServerLevel level)
            level.ChunkSource.RemovePlayerTickets(player);
    }

    //OfflineUuid the offline-mode player uuid, aligned with vanilla UUID.nameUUIDFromBytes("OfflinePlayer:" + name)
    private static Guid OfflineUuid(string name)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"OfflinePlayer:{name}"));
        //Version 3 (MD5) and variant bit correction, then the 128 bits of a Java UUID restored big-endian
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash, bigEndian: true);
    }
}

//DebugPlayer a single fake player holding the player object produced by the real join flow and its fake connection
public sealed class DebugPlayer
{
    //_walkTarget the stepping walk destination; null means not walking
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

    //IsWalking whether it is currently stepping walking
    public bool IsWalking => _walkTarget is not null;

    //StartWalk steps toward the destination; interval is the ticks per step, 1 means one step per tick
    public void StartWalk(Vec3 target, int interval)
    {
        _walkTarget = target;
        _walkInterval = Math.Max(1, interval);
        _walkCooldown = 0;
    }

    //StopWalk interrupts the current walk
    public void StopWalk() => _walkTarget = null;

    //Tick advances one step toward the destination per tick and stops on arrival
    //Also replies to the heartbeat for the fake client; a real client replies via netty, and an unanswered fake connection is kicked for timeout
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
        //The facing follows the direction of travel, head and body turn together; the client model refreshes from the move packet and head rotation packet
        if (dx != 0 || dz != 0)
            Player.Yaw = (float)(Math.Atan2(-dx, dz) * (180.0 / Math.PI));
    }
}
