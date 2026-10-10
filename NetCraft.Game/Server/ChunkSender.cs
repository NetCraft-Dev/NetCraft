using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Storage.Light;

namespace NetCraft.Game.Server;

//ChunkSender progressive chunk sender, maps to vanilla PlayerChunkSender
//UpdateCenter updates the view center when a player crosses a chunk; chunks entering view are queued and those leaving are forgotten, maps to vanilla updateChunkTracking+applyChunkTrackingView
//Tick sends a batch of BatchStart + ready chunks + BatchFinished per tick; not-ready ones stay in the pending set for the next tick
//Generation advances in the ServerChunkCache background without blocking the main loop; it terminates permanently after disconnect
public sealed class ChunkSender
{
    //ChunksPerTick chunks sent per tick, aligned with the order of magnitude of vanilla desiredChunksPerTick
    public const int ChunksPerTick = 4;

    private readonly Connection _connection;
    private readonly Func<ChunkPos, ChunkAccess?> _provider;
    private readonly Func<ChunkPos, bool>? _isFailed;
    //_lightSource light data source; empty sends empty light
    private readonly ServerChunkCache? _lightSource;
    //_blockEntityBridge block entity data source; empty means the chunk packet carries no block entities
    private readonly IBlockEntityBridge? _blockEntityBridge;
    //Pending coordinates in queue order; each tick takes a batch of ready ones from the front
    //Vanilla pendingChunks is also an insertion-order queue; this project scanning everything and sorting every tick was pure overhead
    private readonly List<long> _pending = new();
    private readonly HashSet<long> _pendingSet = new();
    //Coordinates already queued within view, including sent ones; used when leaving view to decide whether to send Forget
    private readonly HashSet<long> _tracked = new();
    private int _centerX = int.MinValue;
    private int _centerZ;
    private int _viewDistance = 1;
    //_cursor the scan start for the next round; not-ready chunks stay in place and are not re-scanned from the front
    private int _cursor;
    private int _sentCount;
    private int _failedCount;
    private bool _closed;

    public ChunkSender(Connection connection, Func<ChunkPos, ChunkAccess?> provider,
        Func<ChunkPos, bool>? isFailed = null, ServerChunkCache? lightSource = null,
        IBlockEntityBridge? blockEntityBridge = null)
    {
        _connection = connection;
        _provider = provider;
        _isFailed = isFailed;
        _lightSource = lightSource;
        _blockEntityBridge = blockEntityBridge;
    }

    //PendingCount number of chunks waiting to be sent, for diagnostics
    public int PendingCount => _pending.Count;

    //FailedCount number of load-failed chunks dropped, for diagnostics
    public int FailedCount => _failedCount;

    //Temporary instrumentation: distinguishes "the sender is never handed a new view center" from "it is, but nothing enters view"
    public static long DiagnosticUpdateCenterCalls;
    public static long DiagnosticCenterMoves;
    public static long DiagnosticChunksQueued;
    //Which early exit the sender takes: a queue with entries but no work at all means one of the first two
    public static long DiagnosticTickClosed;
    public static long DiagnosticTickDisconnected;
    public static long DiagnosticTickEmpty;

    //UpdateCenter updates the view center; chunks entering view are queued and leaving ones forgotten, and SetChunkCacheCenter is synced
    //Returns early when neither the center nor the view distance changed, to avoid resending packets
    public void UpdateCenter(int centerChunkX, int centerChunkZ, int viewDistance)
    {
        var radius = Math.Clamp(viewDistance, 1, 32);
        DiagnosticUpdateCenterCalls++;
        if (_centerX != int.MinValue && _centerX == centerChunkX && _centerZ == centerChunkZ && _viewDistance == radius)
            return;
        DiagnosticCenterMoves++;
        _centerX = centerChunkX;
        _centerZ = centerChunkZ;
        _viewDistance = radius;
        var radiusSquared = radius * radius;
        //Coordinates entering view are queued; the circular test matches the initial queueing rule
        //Within this batch they are sorted by distance before appending, so the queue is roughly distance-ordered and the sender need not re-sort everything every tick
        List<long>? added = null;
        for (var dx = -radius; dx <= radius; dx++)
        for (var dz = -radius; dz <= radius; dz++)
        {
            if (dx * dx + dz * dz > radiusSquared) continue;
            var key = ChunkPos.Pack(centerChunkX + dx, centerChunkZ + dz);
            if (_tracked.Add(key)) (added ??= new List<long>()).Add(key);
        }
        if (added is not null)
        {
            added.Sort((a, b) => DistanceSquared(ChunkPos.Unpack(a)).CompareTo(DistanceSquared(ChunkPos.Unpack(b))));
            foreach (var key in added)
            {
                _pending.Add(key);
                _pendingSet.Add(key);
                DiagnosticChunksQueued++;
            }
        }
        //Forgetting on leaving view: unsent ones are only dequeued, sent ones send a Forget packet, maps to vanilla dropChunk
        foreach (var key in _tracked.Where(key => DistanceSquared(ChunkPos.Unpack(key)) > radiusSquared).ToList())
            DropChunk(key);
        _connection.Send(new ClientboundSetChunkCacheCenterPacket(centerChunkX, centerChunkZ));
    }

    //DropChunk forgets a single chunk; only one already sent needs to notify the client
    private void DropChunk(long key)
    {
        _tracked.Remove(key);
        if (_pendingSet.Remove(key))
        {
            _pending.Remove(key);
            return;
        }
        if (_connection.IsConnected)
            _connection.Send(new ClientboundForgetLevelChunkPacket(ChunkPos.Unpack(key)));
    }

    private int DistanceSquared(ChunkPos pos)
    {
        var dx = pos.X - _centerX;
        var dz = pos.Z - _centerZ;
        return dx * dx + dz * dz;
    }

    //Tick progressively sends this batch of ready chunks, taking a batch from the cursor in queue order
    //The queue is roughly distance-ordered on enqueue; the per-tick scan is capped by the budget and not-ready ones stay in place for the cursor to revisit
    public void Tick()
    {
        if (_closed) { DiagnosticTickClosed++; return; }
        //If the connection is closed, sending terminates and state is cleared, avoiding an ObjectDisposedException every tick flooding the log
        if (!_connection.IsConnected)
        {
            DiagnosticTickDisconnected++;
            Log.Debug($"Chunk sending aborted, connection closed remaining={_pending.Count}");
            ClearQueue();
            _closed = true;
            return;
        }
        if (_pending.Count == 0)
        {
            DiagnosticTickEmpty++;
            _cursor = 0;
            return;
        }
        if (_cursor >= _pending.Count) _cursor = 0;
        var budget = Math.Max(ChunksPerTick * 4, 16);
        var batch = new List<(ChunkPos Pos, ChunkAccess Chunk)>(ChunksPerTick);
        var index = _cursor;
        var scanStart = TickStageProfiler.Now();
        while (index < _pending.Count && budget-- > 0 && batch.Count < ChunksPerTick)
        {
            var key = _pending[index];
            var pos = ChunkPos.Unpack(key);
            if (_isFailed?.Invoke(pos) == true)
            {
                _pending.RemoveAt(index);
                _pendingSet.Remove(key);
                _tracked.Remove(key);
                _failedCount++;
                continue;
            }
            var chunk = _provider(pos);
            if (chunk is null)
            {
                index++;
                continue;
            }
            //A chunk whose light has not been built yet stays queued for a later tick
            //Sending it now would leave the client with a black chunk until an incremental light update arrived for every section
            if (_lightSource is not null && !_lightSource.IsLightReady(pos))
            {
                index++;
                continue;
            }
            batch.Add((pos, chunk));
            _pending.RemoveAt(index);
            _pendingSet.Remove(key);
        }
        TickStageProfiler.Record(TickStage.ChunkScan, scanStart);
        _cursor = index >= _pending.Count ? 0 : index;
        if (batch.Count == 0) return;
        try
        {
            _connection.Send(new ClientboundChunkBatchStartPacket());
            foreach (var (pos, chunk) in batch)
            {
                var prepStart = TickStageProfiler.Now();
                //Chunk serialization is heavy and does not touch the light engine, so it runs outside the light lock
                //The light data itself is read straight off the published snapshot, so building the packet needs no lock at all
                var lightSource = _lightSource;
                //Block entities are sent with the chunk so the client sees existing block entities on join
                var blockEntities = _blockEntityBridge?.Collect(pos);
                var packet = lightSource is null
                    ? ClientboundLevelChunkWithLightPacket.CreatePrepared(
                        pos.X, pos.Z, chunk, () => ClientboundLightUpdatePacketData.Empty, blockEntities)
                    : ClientboundLevelChunkWithLightPacket.CreatePrepared(
                        pos.X, pos.Z, chunk,
                        () => new ClientboundLightUpdatePacketData(pos, lightSource.LightEngine),
                        blockEntities);
                TickStageProfiler.Record(TickStage.ChunkPrep, prepStart);

                var sendStart = TickStageProfiler.Now();
                _connection.Send(packet);
                TickStageProfiler.Record(TickStage.ChunkSend, sendStart);
                _sentCount++;
            }
            _connection.Send(new ClientboundChunkBatchFinishedPacket(batch.Count));
        }
        catch (Exception e)
        {
            _closed = true;
            //The connection being closed by the peer during sending is a normal race; terminate silently without a false report
            if (!_connection.IsConnected)
            {
                Log.Debug($"Chunk sending aborted, connection closed remaining={_pending.Count}");
                ClearQueue();
                return;
            }
            Log.Warning($"Chunk send failed remaining={_pending.Count} {e.Message} connAlive={_connection.IsConnected} details={_connection.DisconnectionDetails?.Reason ?? "none"}");
        }
    }

    private void ClearQueue()
    {
        _pending.Clear();
        _pendingSet.Clear();
        _tracked.Clear();
        _cursor = 0;
    }
}
