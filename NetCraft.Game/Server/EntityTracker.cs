using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util;
//属性实例类型自带命名空间 这里只取这两个名字
using AttributeInstance = NetCraft.Registry.EntityAttribute.AttributeInstance;
using AttributeModifier = NetCraft.Registry.EntityAttribute.AttributeModifier;

namespace NetCraft.Game.Server;

//EntityTracker 实体追踪器对应原版 ChunkMap.TrackedEntity 集合
//按玩家视距维护可见实体集合 进入视距下发 AddEntity 离开视距下发 RemoveEntities
//位置朝向变化每 tick 下发移动包 位移超阈值改走位置同步包
//玩家额外同步头部朝向与姿态数据 否则别人看到的模型头不转也看不到疾跑潜行
public sealed class EntityTracker
{
    //TeleportThreshold 单个轴位移超过该值改用传送包 对应原版 8 格
    public const double TeleportThreshold = 8.0;

    //RotationTolerance 角度变化小于该值不下发旋转包 对应原版 1 度
    public const float RotationTolerance = 1f;

    //SharedFlagsIndex 实体数据共享标志位索引 与 Registry.Entity 的常量同源
    public const byte SharedFlagsIndex = NetCraft.Registry.Entity.SharedFlagsIndex;

    //PoseIndex 实体数据姿态索引 与 Registry.Entity 的常量同源
    public const byte PoseIndex = NetCraft.Registry.Entity.PoseIndex;

    //DeltaScale 相对位移包的定点精度 对应原版 VecDeltaCodec 的 4096 步
    private const double DeltaScale = 4096.0;

    //_tracked 实体 id 到追踪状态
    private readonly Dictionary<int, TrackedEntity> _tracked = new();

    //_seenByPlayer 玩家实体 id 到它已配上对的实体 id 集合
    //PruneStale 原先每玩家遍历整个 _tracked 是 O(玩家数×实体数) 有反查后只比对自己见过的
    private readonly Dictionary<int, HashSet<int>> _seenByPlayer = new();

    //BucketShift 空间分桶的区块对数 一格 16x16 区块可覆盖最大视距 32
    private const int BucketShift = 4;

    //TrackedEntity 单个实体的追踪状态
    private sealed class TrackedEntity
    {
        //Observers 每个观察者各自一份发送记账
        //同一 tick 内多个玩家都能看到同一实体 记账放全局一份会让先处理的玩家把变化吃掉
        public Dictionary<int, ObserverState> Observers { get; } = new();
    }

    //ObserverState 某个观察者对某个实体的上次发送状态
    private sealed class ObserverState
    {
        public required Vec3 LastPos { get; set; }
        public required float LastYRot { get; set; }
        public required float LastXRot { get; set; }
        public required bool LastOnGround { get; set; }
        public required float LastHeadYRot { get; set; }

        //LastSyncedVersion 上次给该观察者下发元数据时的版本号 版本没变就不重发
        public int LastSyncedVersion { get; set; } = -1;
    }

    //Tick 推进所有玩家的实体追踪并逐包发送
    //candidates 以外的已追踪实体视为移出世界 向仍跟踪它的玩家补发移除包
    //候选按空间分桶 每个玩家只遍历自己周围 3x3 格的实体 不再逐个玩家扫全场
    public void Tick(PersistentServerLevel level, IReadOnlyList<ServerPlayer> players)
    {
        if (players.Count == 0) return;
        var candidates = CollectCandidates(level, players);
        var buckets = BucketByArea(candidates);
        var nearby = new List<ITrackedEntity>();
        foreach (var player in players)
        {
            if (!player.Connection.IsConnected) continue;
            nearby.Clear();
            CollectNearby(player, buckets, nearby);
            foreach (var packet in Sync(player, nearby))
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
        }
        //本 tick 所有观察者都发过之后再清脏属性 对应原版 ServerEntity 广播后的 clear
        //在 Sync 里清会让先处理的玩家把变化吃掉 后面的观察者一个属性包都收不到
        foreach (var entity in candidates) entity.Attributes?.ClearAttributesToSync();
    }

    //BucketByArea 按 16x16 区块的空间格子给候选分桶 键是格子坐标
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

    //CollectNearby 取玩家周围覆盖其视距的格子里的实体
    //格子边长 16 区块 视距 R 区块需取 R/16+1 格半径 默认视距 12 时就是 3x3 格
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

    //BucketKey 世界坐标落在哪个 16x16 区块的空间格
    private static long BucketKey(double x, double z)
        => (((long)Math.Floor(x) >> (BucketShift + 4)) << 32) ^ (((long)Math.Floor(z) >> (BucketShift + 4)) & 0xFFFFFFFFL);

    //Sync 计算该玩家本帧需要收到的实体包 供测试直接断言
    //位置变化每 tick 结算 对齐原版 ServerEntity.sendChanges 每 tick 调用的语义
    //candidates 是本帧该玩家视野候选 不在其中的已配对实体按原版 TrackedEntity.updatePlayer 的不可见分支摘掉
    public List<Packet<ClientGamePacketListener>> Sync(ServerPlayer player, IReadOnlyList<ITrackedEntity> candidates)
    {
        var packets = new List<Packet<ClientGamePacketListener>>();
        //processed 本帧实际遍历到的实体 id 集合 PruneStale 据此判断玩家是否还看得见
        var processed = new HashSet<int>(candidates.Count);
        foreach (var entity in candidates)
        {
            if (entity.Type is null) continue;
            processed.Add(entity.EntityId);
            //玩家自身不发自己的实体包 对应原版 TrackedEntity.updatePlayer 的 self 跳过
            if (ReferenceEquals(entity, player)) continue;
            if (!_tracked.TryGetValue(entity.EntityId, out var state))
            {
                state = new TrackedEntity();
                _tracked[entity.EntityId] = state;
            }
            var seen = state.Observers.ContainsKey(player.EntityId);
            if (!IsVisible(player, entity))
            {
                if (seen)
                {
                    state.Observers.Remove(player.EntityId);
                    SeenOf(player.EntityId).Remove(entity.EntityId);
                    packets.Add(new ClientboundRemoveEntitiesPacket(new[] { entity.EntityId }));
                }
                continue;
            }
            if (!seen)
            {
                var observer = new ObserverState
                {
                    LastPos = entity.Pos,
                    LastYRot = entity.YRot,
                    LastXRot = entity.XRot,
                    LastOnGround = entity.OnGround,
                    LastHeadYRot = entity.YRot,
                };
                state.Observers[player.EntityId] = observer;
                SeenOf(player.EntityId).Add(entity.EntityId);
                packets.Add(BuildAddEntity(entity));
                //配对时把元数据全量下发 玩家要有姿态 掉落物要有物品栈 客户端建好模型就是对的
                if (entity is ISyncedEntity synced)
                {
                    var values = synced.SyncedData.CollectAll();
                    observer.LastSyncedVersion = synced.SyncedData.Version;
                    if (values.Count > 0) packets.Add(BuildSyncedData(entity.EntityId, values));
                }
                //配对时把可同步属性全量下发 对应原版 ServerEntity.sendPairingData 的属性分支
                if (entity.Attributes is { } attributes && attributes.SyncableAttributes.Count > 0)
                    packets.Add(BuildAttributes(entity.EntityId, attributes.SyncableAttributes));
                continue;
            }
            var observed = state.Observers[player.EntityId];
            AddMovementPackets(observed, entity, packets);
            AddHeadRotationPacket(observed, entity, packets);
            AddSyncedDataPacket(observed, entity, packets);
            AddAttributesPacket(entity, packets);
        }
        PruneStale(player, processed, packets);
        return packets;
    }

    //CollectCandidates 收集可追踪对象 世界实体加在线玩家
    private static List<ITrackedEntity> CollectCandidates(PersistentServerLevel level, IReadOnlyList<ServerPlayer> players)
    {
        var list = new List<ITrackedEntity>();
        foreach (var entity in level.Entities) list.Add(entity);
        foreach (var player in players) list.Add(player);
        return list;
    }

    //BuildAddEntity 组装添加实体包 朝向按原版压缩成单字节角度
    //第三个角度是头部朝向 本作头随身体与第二个角度相同
    private static ClientboundAddEntityPacket BuildAddEntity(ITrackedEntity entity)
        => new(entity.EntityId, entity.Uuid, entity.Type!, entity.Pos.X, entity.Pos.Y, entity.Pos.Z,
            entity.Velocity, Mth.PackDegrees(entity.XRot), Mth.PackDegrees(entity.YRot),
            Mth.PackDegrees(entity.YRot), 0);

    //AddMovementPackets 按位移与朝向变化组装移动包
    //位移超阈值走传送包 阈值内走相对位移包 仅朝向变化走旋转包
    private static void AddMovementPackets(ObserverState state, ITrackedEntity entity,
        List<Packet<ClientGamePacketListener>> packets)
    {
        var dx = entity.Pos.X - state.LastPos.X;
        var dy = entity.Pos.Y - state.LastPos.Y;
        var dz = entity.Pos.Z - state.LastPos.Z;
        var moved = dx != 0 || dy != 0 || dz != 0;
        var yRotChanged = Mth.Abs(Mth.WrapDegrees(entity.YRot - state.LastYRot)) >= RotationTolerance;
        var xRotChanged = Mth.Abs(Mth.WrapDegrees(entity.XRot - state.LastXRot)) >= RotationTolerance;
        var onGroundChanged = entity.OnGround != state.LastOnGround;
        if (!moved && !yRotChanged && !xRotChanged && !onGroundChanged) return;

        var yRot = Mth.PackDegrees(entity.YRot);
        var xRot = Mth.PackDegrees(entity.XRot);
        if (moved && (Math.Abs(dx) > TeleportThreshold || Math.Abs(dy) > TeleportThreshold || Math.Abs(dz) > TeleportThreshold))
        {
            //大位移走位置同步包 客户端处理时同步重置位置基准 VecDeltaCodec
            //用传送包的话客户端只做插值不重置基准 之后每个增量包都基于旧基准累加
            //观察者端模型的偏移量会固定等于这次位移 也就是传送后一动就飞出去且距离不变
            packets.Add(new ClientboundEntityPositionSyncPacket(entity.EntityId, entity.Pos, entity.Velocity,
                entity.YRot, entity.XRot, entity.OnGround));
        }
        else if (moved)
        {
            var xa = (short)(EncodeDelta(entity.Pos.X) - EncodeDelta(state.LastPos.X));
            var ya = (short)(EncodeDelta(entity.Pos.Y) - EncodeDelta(state.LastPos.Y));
            var za = (short)(EncodeDelta(entity.Pos.Z) - EncodeDelta(state.LastPos.Z));
            if (yRotChanged || xRotChanged)
                packets.Add(new ClientboundMoveEntityPacket.PosRot(entity.EntityId, xa, ya, za, yRot, xRot, entity.OnGround));
            else
                packets.Add(new ClientboundMoveEntityPacket.Pos(entity.EntityId, xa, ya, za, entity.OnGround));
        }
        else
        {
            packets.Add(new ClientboundMoveEntityPacket.Rot(entity.EntityId, yRot, xRot, entity.OnGround));
        }
        state.LastPos = entity.Pos;
        state.LastYRot = entity.YRot;
        state.LastXRot = entity.XRot;
        state.LastOnGround = entity.OnGround;
    }

    //AddHeadRotationPacket 头部朝向变化下发头部旋转包 对应原版 ServerEntity 的 rotateHead 分支
    //客户端模型的头只认这个包 只发移动旋转包的话别人看你转头时头不动
    //本作没有独立的头部转向控制 头部朝向跟随身体朝向
    private static void AddHeadRotationPacket(ObserverState state, ITrackedEntity entity,
        List<Packet<ClientGamePacketListener>> packets)
    {
        if (Mth.Abs(Mth.WrapDegrees(entity.YRot - state.LastHeadYRot)) < RotationTolerance) return;
        state.LastHeadYRot = entity.YRot;
        packets.Add(new ClientboundRotateHeadPacket(entity.EntityId, Mth.PackDegrees(entity.YRot)));
    }

    //AddSyncedDataPacket 元数据版本变化后下发 对应原版 ServerEntity 的同步数据分支
    //版本号按观察者各记一份 多观察者下先处理的玩家不会把变化吃掉
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

    //BuildSyncedData 把内核的元数据条目转成实体数据包
    private static ClientboundSetEntityDataPacket BuildSyncedData(int entityId, List<SynchedValue> values)
    {
        var items = new EntityDataItem[values.Count];
        for (var i = 0; i < values.Count; i++)
            items[i] = new EntityDataItem(values[i].Index, values[i].SerializerId, values[i].Value);
        return new ClientboundSetEntityDataPacket(entityId, items);
    }

    //AddAttributesPacket 属性被改脏后补发 对应原版 ServerEntity.sendDirtyEntityData 的属性分支
    //脏集合不在这里清 本 tick 内其它观察者还要读它 统一由 Tick 收尾清理
    private static void AddAttributesPacket(ITrackedEntity entity, List<Packet<ClientGamePacketListener>> packets)
    {
        if (entity.Attributes is not { } attributes) return;
        var dirty = attributes.AttributesToSync;
        if (dirty.Count == 0) return;
        packets.Add(BuildAttributes(entity.EntityId, dirty));
    }

    //BuildAttributes 把属性实例转成网络快照 基值与全部修饰符一起带上
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

    //PruneStale 处理本帧已不在此玩家视野候选内的已配对实体 补发移除包后摘掉配对
    //判据必须是本帧遍历到的实体而不是全局存活集合:
    //玩家被传送或走远后不再落进对方的候选分桶 用全局存活判断会认为它还在 移除包永远发不出去
    //对方客户端上的模型就会冻在最后一次同步的位置 只有重生/重进/再靠近才恢复
    //只遍历该玩家自己见过的实体 不再每个玩家都扫一遍全表
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
            //没人再跟踪该实体就丢弃它的追踪状态
            if (state.Observers.Count == 0) _tracked.Remove(id);
        }
    }

    //SeenOf 取玩家已配上对的实体 id 集合 没有就建一个
    private HashSet<int> SeenOf(int playerEntityId)
    {
        if (!_seenByPlayer.TryGetValue(playerEntityId, out var seen))
            _seenByPlayer[playerEntityId] = seen = new HashSet<int>();
        return seen;
    }

    //ForgetPlayer 玩家离开时清理它的可见记录与别人对它实体的跟踪
    //玩家走了以后没人再推进它的 PruneStale 不主动清会一直留在表里
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

    //IsVisible 按玩家视距与实体追踪距离的较小者做水平距离判定
    private static bool IsVisible(ServerPlayer player, ITrackedEntity entity)
    {
        var rangeChunks = Math.Min(entity.Type?.TrackingRangeChunks ?? 0, player.ViewDistanceChunks);
        if (rangeChunks <= 0) return false;
        var range = rangeChunks * 16.0;
        var dx = player.Position.X - entity.Pos.X;
        var dz = player.Position.Z - entity.Pos.Z;
        return dx * dx + dz * dz <= range * range;
    }

    //EncodeDelta 位置按 1/4096 格量化 对应原版 VecDeltaCodec.encode
    private static long EncodeDelta(double value) => (long)Math.Round(value * DeltaScale);
}
