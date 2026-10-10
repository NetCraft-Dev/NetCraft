using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util;
//The attribute instance type carries its own namespace; only these two names are taken here
using AttributeInstance = NetCraft.Registry.EntityAttribute.AttributeInstance;
using AttributeModifier = NetCraft.Registry.EntityAttribute.AttributeModifier;

namespace NetCraft.Game.Server;

//EntityTracker entity tracker, maps to the vanilla ChunkMap.TrackedEntity set
//Maintains the visible entity set by player view distance: AddEntity on entering view, RemoveEntities on leaving
//Position/facing changes send a move packet every tick; a displacement over the threshold switches to a position sync packet
//Players additionally sync head facing and pose data, or other players see a model whose head does not turn and cannot see sprinting/sneaking
public sealed class EntityTracker
{
    //TeleportThreshold a displacement over this on a single axis switches to the teleport packet, maps to vanilla 8 blocks
    public const double TeleportThreshold = 8.0;

    //RotationTolerance an angle change below this sends no rotation packet, maps to vanilla 1 degree
    public const float RotationTolerance = 1f;

    //SharedFlagsIndex entity data shared flags index, from the same source as the Registry.Entity constant
    public const byte SharedFlagsIndex = NetCraft.Registry.Entity.SharedFlagsIndex;

    //PoseIndex entity data pose index, from the same source as the Registry.Entity constant
    public const byte PoseIndex = NetCraft.Registry.Entity.PoseIndex;

    //DeltaScale fixed-point precision of the relative displacement packet, maps to vanilla VecDeltaCodec's 4096 steps
    private const double DeltaScale = 4096.0;

    //_tracked entity id to tracking state
    private readonly Dictionary<int, TrackedEntity> _tracked = new();

    //_seenByPlayer player entity id to the set of entity ids it has paired with
    //PruneStale originally iterated the whole _tracked per player (O(players x entities)); with the reverse lookup it only compares what it has seen
    private readonly Dictionary<int, HashSet<int>> _seenByPlayer = new();

    //BucketShift spatial bucket chunk shift; a 16x16 chunk cell covers a max view distance of 32
    private const int BucketShift = 4;

    //Scaling diagnostics, temporary instrumentation removed with the rest of the perf counters
    //Entities is the world entity count, NearbyTotal the sum of every player's candidate list, VisibleTotal how many of
    //those candidate pairs passed IsVisible. NearbyTotal divided by PlayerCount is the average scan per player, and the
    //gap between it and VisibleTotal is what the 16-chunk cell granularity throws away
    public static int DiagnosticEntities;
    public static long DiagnosticNearbyTotal;
    public static long DiagnosticVisibleTotal;
    public static long DiagnosticPlayerCount;
    //Packets built across the window: divides TrackLoop's cost into a per-pair figure and a per-packet one
    public static long DiagnosticPacketTotal;

    //Packet kind counters, temporary instrumentation: shows which kinds produce the per-pair packet count
    //Index order matches DiagnosticPacketNames
    public static readonly long[] DiagnosticPacketKinds = new long[8];
    public static readonly string[] DiagnosticPacketNames =
    {
        "add", "remove", "move", "rot", "sync", "head", "data", "attr",
    };

    //Sync round counter: the packet and visibility totals only divide out correctly if this matches the player count per tick
    public static long DiagnosticSyncCalls;

    //CountPacketKind buckets one built packet, called once per packet at the end of a Sync round
    private static void CountPacketKind(Packet<ClientGamePacketListener> packet)
    {
        switch (packet)
        {
            case ClientboundAddEntityPacket: DiagnosticPacketKinds[0]++; break;
            case ClientboundRemoveEntitiesPacket: DiagnosticPacketKinds[1]++; break;
            case ClientboundMoveEntityPacket.Pos:
            case ClientboundMoveEntityPacket.PosRot: DiagnosticPacketKinds[2]++; break;
            case ClientboundMoveEntityPacket.Rot: DiagnosticPacketKinds[3]++; break;
            case ClientboundEntityPositionSyncPacket: DiagnosticPacketKinds[4]++; break;
            case ClientboundRotateHeadPacket: DiagnosticPacketKinds[5]++; break;
            case ClientboundSetEntityDataPacket: DiagnosticPacketKinds[6]++; break;
            case ClientboundUpdateAttributesPacket: DiagnosticPacketKinds[7]++; break;
        }
    }

    //ResetDiagnostics clears the scaling counters so a perf window reports only its own numbers
    public static void ResetDiagnostics()
    {
        DiagnosticEntities = 0;
        DiagnosticNearbyTotal = 0;
        DiagnosticVisibleTotal = 0;
        DiagnosticPlayerCount = 0;
        DiagnosticPacketTotal = 0;
        DiagnosticSyncCalls = 0;
        Array.Clear(DiagnosticPacketKinds);
    }

    //TrackedEntity the tracking state of a single entity
    private sealed class TrackedEntity
    {
        //Observers each observer has its own send bookkeeping
        //Multiple players can see the same entity in one tick; a single global bookkeeping would let the first player handled eat the change
        public Dictionary<int, ObserverState> Observers { get; } = new();
    }

    //ObserverState an observer's last send state for an entity
    private sealed class ObserverState
    {
        public required Vec3 LastPos { get; set; }
        public required float LastYRot { get; set; }
        public required float LastXRot { get; set; }
        public required bool LastOnGround { get; set; }
        public required float LastHeadYRot { get; set; }

        //LastSyncedVersion the version number when metadata was last sent to this observer; not resent when unchanged
        public int LastSyncedVersion { get; set; } = -1;
    }

    //EntitySnapshot the entity fields one Sync round needs, captured once per pair
    //Every member is an interface property call, and the round used to read most of them three to five times each; measured at roughly a
    //third of the loop body, so the values are copied out here and the loop and its helpers work off the local copy
    private readonly struct EntitySnapshot
    {
        public readonly int EntityId;
        public readonly EntityType<object> Type;
        public readonly Guid Uuid;
        public readonly Vec3 Pos;
        public readonly Vec3 Velocity;
        public readonly float YRot;
        public readonly float XRot;
        public readonly bool OnGround;

        //EntityId and Type are passed in because the caller already read them to reject unusable candidates
        public EntitySnapshot(ITrackedEntity entity, int entityId, EntityType<object> type)
        {
            EntityId = entityId;
            Type = type;
            Uuid = entity.Uuid;
            Pos = entity.Pos;
            Velocity = entity.Velocity;
            YRot = entity.YRot;
            XRot = entity.XRot;
            OnGround = entity.OnGround;
        }
    }

    //Tick advances all players' entity tracking and sends packet by packet
    //Tracked entities outside candidates are treated as removed from the world; a removal packet is sent to players still tracking it
    //Candidates are spatially bucketed; each player only iterates entities in its own surrounding 3x3 cells instead of scanning the whole field per player
    public void Tick(PersistentServerLevel level, IReadOnlyList<ServerPlayer> players)
    {
        if (players.Count == 0) return;
        var prepStart = TickStageProfiler.Now();
        var candidates = CollectCandidates(level, players);
        DiagnosticEntities = candidates.Count;
        var buckets = BucketByArea(candidates);
        TickStageProfiler.Record(TickStage.TrackPrep, prepStart);

        var nearby = new List<ITrackedEntity>();
        foreach (var player in players)
        {
            if (!player.Connection.IsConnected) continue;
            nearby.Clear();
            var syncStart = TickStageProfiler.Now();
            CollectNearby(player, buckets, nearby);
            DiagnosticNearbyTotal += nearby.Count;
            var packets = Sync(player, nearby);
            TickStageProfiler.Record(TickStage.TrackSync, syncStart);

            var sendStart = TickStageProfiler.Now();
            foreach (var packet in packets)
            {
                try
                {
                    player.Connection.Send(packet);
                }
                catch (Exception e)
                {
                    Log.Warning($"Entity sync packet failed player={player.Profile.Name} {e.Message}");
                }
            }
            TickStageProfiler.Record(TickStage.TrackSend, sendStart);
        }
        //Clear dirty attributes only after all observers have sent this tick, maps to vanilla ServerEntity's clear after broadcasting
        //Clearing inside Sync would let the first player handled eat the change and later observers receive no attribute packet
        foreach (var entity in candidates) entity.Attributes?.ClearAttributesToSync();
        DiagnosticPlayerCount += players.Count;
    }

    //BucketByArea buckets candidates into 16x16-chunk spatial cells; the key is the cell coordinate
    private static Dictionary<long, List<ITrackedEntity>> BucketByArea(List<ITrackedEntity> candidates)
    {
        var buckets = new Dictionary<long, List<ITrackedEntity>>();
        foreach (var entity in candidates)
        {
            var key = BucketKey(entity.Pos.X, entity.Pos.Z);
            if (!buckets.TryGetValue(key, out var list))
                buckets[key] = list = new List<ITrackedEntity>(4);
            list.Add(entity);
        }
        return buckets;
    }

    //CollectNearby takes entities in the cells around a player covering its view distance
    //The cell side is 16 chunks; a view distance of R chunks needs cells within R/16+1, i.e. 3x3 cells at the default view distance 12
    private static void CollectNearby(ServerPlayer player, Dictionary<long, List<ITrackedEntity>> buckets,
        List<ITrackedEntity> result)
    {
        var radius = Math.Clamp(player.ViewDistanceChunks, 1, 64) / 16 + 1;
        var baseX = (long)Math.Floor(player.Position.X) >> (BucketShift + 4);
        var baseZ = (long)Math.Floor(player.Position.Z) >> (BucketShift + 4);
        for (var dx = -radius; dx <= radius; dx++)
        for (var dz = -radius; dz <= radius; dz++)
            if (buckets.TryGetValue(((baseX + dx) << 32) ^ ((baseZ + dz) & 0xFFFFFFFFL), out var list))
                result.AddRange(list);
    }

    //BucketKey which 16x16-chunk spatial cell a world coordinate falls in
    private static long BucketKey(double x, double z)
        => (((long)Math.Floor(x) >> (BucketShift + 4)) << 32) ^ (((long)Math.Floor(z) >> (BucketShift + 4)) & 0xFFFFFFFFL);

    //Sync computes the entity packets the player should receive this frame, for tests to assert directly
    //Position changes settle every tick, aligned with the semantics of vanilla ServerEntity.sendChanges called every tick
    //candidates are the player's view candidates this frame; paired entities not among them are dropped like the invisible branch of vanilla TrackedEntity.updatePlayer
    public List<Packet<ClientGamePacketListener>> Sync(ServerPlayer player, IReadOnlyList<ITrackedEntity> candidates)
    {
        var packets = new List<Packet<ClientGamePacketListener>>();
        DiagnosticSyncCalls++;
        //processed the entity ids actually iterated this frame; PruneStale uses it to decide whether the player can still see them
        var processed = new HashSet<int>(candidates.Count);
        //Per-player constants hoisted out of the pair loop: Position returns a Vec3 by value and is an interface call, so reading
        //it once here saves a copy and a call on every candidate
        var playerEntityId = player.EntityId;
        var viewDistance = player.ViewDistanceChunks;
        var playerPos = player.Position;
        var playerX = playerPos.X;
        var playerZ = playerPos.Z;
        var loopStart = TickStageProfiler.Now();
        foreach (var entity in candidates)
        {
            var type = entity.Type;
            if (type is null) continue;
            var entityId = entity.EntityId;
            processed.Add(entityId);
            //A player does not send its own entity packet, maps to the self skip of vanilla TrackedEntity.updatePlayer
            if (ReferenceEquals(entity, player)) continue;
            if (!_tracked.TryGetValue(entityId, out var state))
            {
                state = new TrackedEntity();
                _tracked[entityId] = state;
            }
            //One dictionary lookup instead of ContainsKey followed by an indexer read
            var seen = state.Observers.TryGetValue(playerEntityId, out var observed);
            if (!IsVisible(entity, type, playerX, playerZ, viewDistance))
            {
                if (seen)
                {
                    state.Observers.Remove(playerEntityId);
                    SeenOf(playerEntityId).Remove(entityId);
                    packets.Add(new ClientboundRemoveEntitiesPacket(new[] { entityId }));
                }
                continue;
            }
            //Temporary: counts the candidate pairs that survive the distance test; everything above is what the 16-chunk cell granularity makes it scan for nothing
            DiagnosticVisibleTotal++;
            //Built only for pairs that survive the distance test: the invisible branch above is a third of all candidates
            var snapshot = new EntitySnapshot(entity, entityId, type);
            if (!seen)
            {
                var observer = new ObserverState
                {
                    LastPos = snapshot.Pos,
                    LastYRot = snapshot.YRot,
                    LastXRot = snapshot.XRot,
                    LastOnGround = snapshot.OnGround,
                    LastHeadYRot = snapshot.YRot,
                };
                state.Observers[playerEntityId] = observer;
                SeenOf(playerEntityId).Add(entityId);
                packets.Add(BuildAddEntity(snapshot));
                //When pairing, metadata is fully sent; players need a pose and drops need an item stack, so the client builds the right model
                if (entity is ISyncedEntity synced)
                {
                    var values = synced.SyncedData.CollectAll();
                    observer.LastSyncedVersion = synced.SyncedData.Version;
                    if (values.Count > 0) packets.Add(BuildSyncedData(entity.EntityId, values));
                }
                //When pairing, all syncable attributes are sent, maps to the attribute branch of vanilla ServerEntity.sendPairingData
                if (entity.Attributes is { } attributes && attributes.SyncableAttributes.Count > 0)
                    packets.Add(BuildAttributes(entity.EntityId, attributes.SyncableAttributes));
                continue;
            }
            //seen guarantees observed was found; the compiler cannot see the link through TryGetValue's out parameter
            var observerState = observed!;
            AddMovementPackets(observerState, snapshot, packets);
            AddHeadRotationPacket(observerState, snapshot, packets);
            AddSyncedDataPacket(observerState, entity, packets);
            AddAttributesPacket(entity, packets);
        }
        TickStageProfiler.Record(TickStage.TrackLoop, loopStart);
        DiagnosticPacketTotal += packets.Count;
        foreach (var packet in packets) CountPacketKind(packet);
        var pruneStart = TickStageProfiler.Now();
        PruneStale(player, processed, packets);
        TickStageProfiler.Record(TickStage.TrackPrune, pruneStart);
        return packets;
    }

    //CollectCandidates collects traceable objects: world entities plus online players
    private static List<ITrackedEntity> CollectCandidates(PersistentServerLevel level, IReadOnlyList<ServerPlayer> players)
    {
        var list = new List<ITrackedEntity>();
        foreach (var entity in level.Entities) list.Add(entity);
        foreach (var player in players) list.Add(player);
        return list;
    }

    //BuildAddEntity assembles the add entity packet; the facing is compressed to a single-byte angle like vanilla
    //The third angle is the head facing; this project's head follows the body so it equals the second angle
    private static ClientboundAddEntityPacket BuildAddEntity(in EntitySnapshot snapshot)
        => new(snapshot.EntityId, snapshot.Uuid, snapshot.Type, snapshot.Pos.X, snapshot.Pos.Y, snapshot.Pos.Z,
            snapshot.Velocity, Mth.PackDegrees(snapshot.XRot), Mth.PackDegrees(snapshot.YRot),
            Mth.PackDegrees(snapshot.YRot), 0);

    //AddMovementPackets assembles the move packet from displacement and facing changes
    //A displacement over the threshold uses the teleport packet, within it the relative displacement packet, and a facing-only change the rotation packet
    private static void AddMovementPackets(ObserverState state, in EntitySnapshot snapshot,
        List<Packet<ClientGamePacketListener>> packets)
    {
        var pos = snapshot.Pos;
        var dx = pos.X - state.LastPos.X;
        var dy = pos.Y - state.LastPos.Y;
        var dz = pos.Z - state.LastPos.Z;
        var moved = dx != 0 || dy != 0 || dz != 0;
        var yRotChanged = Mth.Abs(Mth.WrapDegrees(snapshot.YRot - state.LastYRot)) >= RotationTolerance;
        var xRotChanged = Mth.Abs(Mth.WrapDegrees(snapshot.XRot - state.LastXRot)) >= RotationTolerance;
        var onGroundChanged = snapshot.OnGround != state.LastOnGround;
        if (!moved && !yRotChanged && !xRotChanged && !onGroundChanged) return;

        var entityId = snapshot.EntityId;
        var yRot = Mth.PackDegrees(snapshot.YRot);
        var xRot = Mth.PackDegrees(snapshot.XRot);
        if (moved && (Math.Abs(dx) > TeleportThreshold || Math.Abs(dy) > TeleportThreshold || Math.Abs(dz) > TeleportThreshold))
        {
            //A large displacement uses the position sync packet; the client resets the position baseline VecDeltaCodec when handling it
            //With the teleport packet the client only interpolates without resetting the baseline; every later delta packet accumulates from the old baseline
            //The observer-side model offset then stays equal to that displacement, i.e. it flies off on the next move after the teleport with a constant distance
            packets.Add(new ClientboundEntityPositionSyncPacket(entityId, pos, snapshot.Velocity,
                snapshot.YRot, snapshot.XRot, snapshot.OnGround));
        }
        else if (moved)
        {
            var xa = (short)(EncodeDelta(pos.X) - EncodeDelta(state.LastPos.X));
            var ya = (short)(EncodeDelta(pos.Y) - EncodeDelta(state.LastPos.Y));
            var za = (short)(EncodeDelta(pos.Z) - EncodeDelta(state.LastPos.Z));
            if (yRotChanged || xRotChanged)
                packets.Add(new ClientboundMoveEntityPacket.PosRot(entityId, xa, ya, za, yRot, xRot, snapshot.OnGround));
            else
                packets.Add(new ClientboundMoveEntityPacket.Pos(entityId, xa, ya, za, snapshot.OnGround));
        }
        else
        {
            packets.Add(new ClientboundMoveEntityPacket.Rot(entityId, yRot, xRot, snapshot.OnGround));
        }
        state.LastPos = pos;
        state.LastYRot = snapshot.YRot;
        state.LastXRot = snapshot.XRot;
        state.LastOnGround = snapshot.OnGround;
    }

    //AddHeadRotationPacket sends the head rotation packet on head facing change, maps to the rotateHead branch of vanilla ServerEntity
    //The client model's head only honors this packet; sending only move/rotate packets leaves the head still when others see you turn
    //This project has no separate head turning control; the head facing follows the body facing
    private static void AddHeadRotationPacket(ObserverState state, in EntitySnapshot snapshot,
        List<Packet<ClientGamePacketListener>> packets)
    {
        if (Mth.Abs(Mth.WrapDegrees(snapshot.YRot - state.LastHeadYRot)) < RotationTolerance) return;
        state.LastHeadYRot = snapshot.YRot;
        packets.Add(new ClientboundRotateHeadPacket(snapshot.EntityId, Mth.PackDegrees(snapshot.YRot)));
    }

    //AddSyncedDataPacket sent after a metadata version change, maps to the synced data branch of vanilla ServerEntity
    //The version number is recorded separately per observer, so with multiple observers the first player handled does not eat the change
    private static void AddSyncedDataPacket(ObserverState state, ITrackedEntity entity,
        List<Packet<ClientGamePacketListener>> packets)
    {
        if (entity is not ISyncedEntity synced) return;
        var data = synced.SyncedData;
        if (state.LastSyncedVersion == data.Version) return;
        state.LastSyncedVersion = data.Version;
        var values = data.CollectAll();
        if (values.Count == 0) return;
        packets.Add(BuildSyncedData(entity.EntityId, values));
    }

    //BuildSyncedData converts the core metadata entries into an entity data packet
    private static ClientboundSetEntityDataPacket BuildSyncedData(int entityId, List<SynchedValue> values)
    {
        var items = new EntityDataItem[values.Count];
        for (var i = 0; i < values.Count; i++)
            items[i] = new EntityDataItem(values[i].Index, values[i].SerializerId, values[i].Value);
        return new ClientboundSetEntityDataPacket(entityId, items);
    }

    //AddAttributesPacket re-sent after attributes are marked dirty, maps to the attribute branch of vanilla ServerEntity.sendDirtyEntityData
    //The dirty set is not cleared here; other observers in this tick still read it and Tick cleans it up at the end
    private static void AddAttributesPacket(ITrackedEntity entity, List<Packet<ClientGamePacketListener>> packets)
    {
        if (entity.Attributes is not { } attributes) return;
        var dirty = attributes.AttributesToSync;
        if (dirty.Count == 0) return;
        packets.Add(BuildAttributes(entity.EntityId, dirty));
    }

    //BuildAttributes converts an attribute instance into a network snapshot, carrying the base value and all modifiers
    private static ClientboundUpdateAttributesPacket BuildAttributes(int entityId,
        IReadOnlyCollection<AttributeInstance> instances)
    {
        var snapshots = new AttributeSnapshot[instances.Count];
        var index = 0;
        foreach (var instance in instances)
        {
            var modifiers = new List<AttributeModifier>(instance.Modifiers);
            snapshots[index++] = new AttributeSnapshot(instance.Attribute, instance.BaseValue, modifiers);
        }
        return new ClientboundUpdateAttributesPacket(entityId, snapshots);
    }

    //PruneStale handles paired entities no longer in this player's view candidates this frame, sending a removal packet then dropping the pair
    //The criterion must be the entities iterated this frame, not the global alive set:
    //After a player is teleported or walks away it no longer falls into the other's candidate buckets; using the global alive set would think it is still there and the removal packet would never be sent
    //The model on the other client would then freeze at the last synced position, recovering only on respawn/rejoin/getting close again
    //Only the entities this player has seen are iterated, no longer scanning the whole table per player
    private void PruneStale(ServerPlayer player, HashSet<int> processed, List<Packet<ClientGamePacketListener>> packets)
    {
        if (!_seenByPlayer.TryGetValue(player.EntityId, out var seen) || seen.Count == 0) return;
        List<int>? gone = null;
        foreach (var id in seen)
        {
            if (processed.Contains(id)) continue;
            (gone ??= new List<int>()).Add(id);
        }
        if (gone is null) return;
        foreach (var id in gone)
        {
            seen.Remove(id);
            packets.Add(new ClientboundRemoveEntitiesPacket(new[] { id }));
            if (!_tracked.TryGetValue(id, out var state)) continue;
            state.Observers.Remove(player.EntityId);
            //When nobody tracks the entity anymore, drop its tracking state
            if (state.Observers.Count == 0) _tracked.Remove(id);
        }
    }

    //SeenOf gets the set of entity ids the player has paired with, creating one if absent
    private HashSet<int> SeenOf(int playerEntityId)
    {
        if (!_seenByPlayer.TryGetValue(playerEntityId, out var seen))
            _seenByPlayer[playerEntityId] = seen = new HashSet<int>();
        return seen;
    }

    //ForgetPlayer cleans up a player's visibility records and others' tracking of its entity when it leaves
    //After the player leaves nobody advances its PruneStale; without active cleanup it would stay in the table
    public void ForgetPlayer(ServerPlayer player)
    {
        _seenByPlayer.Remove(player.EntityId);
        if (!_tracked.TryGetValue(player.EntityId, out var self)) return;
        foreach (var watcherId in self.Observers.Keys)
        {
            if (!_seenByPlayer.TryGetValue(watcherId, out var seen)) continue;
            seen.Remove(player.EntityId);
        }
        self.Observers.Clear();
    }

    //IsVisible does a horizontal distance test with the smaller of the player view distance and the entity tracking distance
    //The player's own position and view distance are passed in rather than read off ServerPlayer: both are loop invariants and
    //Position hands back a Vec3 by value, so reading them per pair copied the same numbers thousands of times a tick
    private static bool IsVisible(ITrackedEntity entity, EntityType<object> type, double playerX, double playerZ,
        int viewDistanceChunks)
    {
        var rangeChunks = Math.Min(type.TrackingRangeChunks, viewDistanceChunks);
        if (rangeChunks <= 0) return false;
        var range = rangeChunks * 16.0;
        var dx = playerX - entity.Pos.X;
        var dz = playerZ - entity.Pos.Z;
        return dx * dx + dz * dz <= range * range;
    }

    //EncodeDelta quantizes the position to 1/4096 of a block, maps to vanilla VecDeltaCodec.encode
    private static long EncodeDelta(double value) => (long)Math.Round(value * DeltaScale);
}
