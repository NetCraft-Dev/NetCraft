using System.Collections.Concurrent;
using System.Threading;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
//属性表类型自带命名空间 这里只取这两个名字
using AttributeMap = NetCraft.Registry.EntityAttribute.AttributeMap;
using AttributeSupplier = NetCraft.Registry.EntityAttribute.AttributeSupplier;
//实体属性默认表在实体命名空间下 只用这一个类型
using DefaultAttributes = NetCraft.Game.World.Entity.DefaultAttributes;

namespace NetCraft.Game.Client.Level;

//ClientLevel 客户端世界对标原版 net.minecraft.client.multiplayer.ClientLevel
//持区块存储 + 光照存储提供 BlockState/BlockLight/SkyLight 查询
//光照独立存储不挂 LevelChunkSection 对齐原版 LevelLightEngine 设计
//W8 加 ReaderWriterLockSlim 保护 section 内部 PalettedContainer SetBlockState 加写锁 dispatcher Build 持读锁
//_chunks/_lights 用 ConcurrentDictionary 字典级线程安全 LoadChunk/UnloadChunk 不加额外锁避免阻塞编译
//SectionDirty 事件写锁外触发防回调内读 ClientLevel 死锁 dispatcher 订阅调 MarkDirty 扩散
public sealed class ClientLevel
{
    //区块存储按 ChunkPos 索引 ConcurrentDictionary 字典级线程安全
    private readonly ConcurrentDictionary<ChunkPos, ChunkAccess> _chunks = new();
    //光照存储按 SectionPos.AsLong 索引 ConcurrentDictionary 字典级线程安全
    private readonly ConcurrentDictionary<long, (DataLayer BlockLight, DataLayer SkyLight)> _lights = new();
    //方块实体状态按 BlockPos.AsLong 索引 服务端下发 ClientboundBlockEntityData 时的本地副本
    private readonly ConcurrentDictionary<long, CompoundTag> _blockEntities = new();
    //移动方块按 BlockPos.AsLong 索引 从方块实体状态里解析出来供渲染层做推进动画
    private readonly ConcurrentDictionary<long, ClientMovingBlock> _movingBlocks = new();
    //实体表按实体 id 索引 服务端实体包下发的本地副本
    private readonly ConcurrentDictionary<int, ClientEntity> _entities = new();
    //_syncRoot 保护 section 内部 PalettedContainer SetBlockState 加写锁 dispatcher Build 持读锁
    private readonly ReaderWriterLockSlim _syncRoot = new();
    //_tickCount 客户端世界累计刻 冻结时不递增 实体动画按它推算存活刻数
    private long _tickCount;
    //_isFrozen 服务端下发的刻速率冻结状态 冻结时本地世界推进停住
    private bool _isFrozen;
    //_stepTicksToRun 冻结下待步进刻数 服务端下发 step 包时写入 走完这些刻仍回到冻结
    private int _stepTicksToRun;
    //_tickRate 服务端下发的每秒刻数 本地世界按它推进刻 对应原版 TickRateManager.tickRate
    //不接这个的话 /tick rate 调慢后客户端仍按 20tps 自顾自推进 活塞这类按刻走的动画会比服务端快几十倍
    private float _tickRate = DefaultTickRate;
    //_tickAccumulator 距下一刻攒下的秒数 逻辑帧与刻率不一致时靠它把钱找齐
    private double _tickAccumulator;

    //DefaultTickRate 客户端初始每秒刻数 与服务端默认一致
    private const float DefaultTickRate = 20f;

    //TickCount 客户端世界累计刻 供渲染层按刻数驱动实体动画
    public long TickCount => _tickCount;

    //IsFrozen 世界是否处于冻结状态
    public bool IsFrozen => _isFrozen;

    //SetTickingState 记录服务端下发的刻速率与冻结状态 对应原版处理 ClientboundTickingStatePacket
    public void SetTickingState(float tickRate, bool frozen)
    {
        _isFrozen = frozen;
        _tickRate = tickRate > 0f ? tickRate : DefaultTickRate;
        if (frozen) return;
        //解冻时清掉残留的步进计数 否则下一次冻结会白送几拍
        _stepTicksToRun = 0;
    }

    //SetTickingStep 记录服务端下发的待步进刻数 对应原版处理 ClientboundTickingStepPacket
    public void SetTickingStep(int tickSteps) => _stepTicksToRun = tickSteps;

    //Tick 客户端世界按真实耗时推进 冻结时停住 实体动画随之不再变化
    //对应原版 TickRateManager.tick: 每凑够一刻先判定这一拍是否推进再递减待步进刻数
    //deltaSeconds 缺省给默认刻长 无帧率信息的调用点按每拍一刻处理
    public void Tick(double deltaSeconds = 1.0 / DefaultTickRate)
    {
        //冻结且没有待步进刻数时不推进 也不攒时间 否则解冻当帧会把冻结期间欠下的刻一口气补上
        if (_isFrozen && _stepTicksToRun <= 0)
        {
            _tickAccumulator = 0;
            return;
        }
        _tickAccumulator += deltaSeconds;
        var interval = 1.0 / _tickRate;
        //单帧最多推 128 刻 防帧率抖动或长时间挂起后一次补跑把主线程卡住
        for (var step = 0; step < 128 && _tickAccumulator >= interval; step++)
        {
            _tickAccumulator -= interval;
            if (_stepTicksToRun > 0) _stepTicksToRun--;
            _tickCount++;
            //移动方块的推进进度按世界走过的刻数算 每前进一步就给它记一刻
            if (!_movingBlocks.IsEmpty)
                foreach (var moving in _movingBlocks.Values) moving.Advance();
            //步进刻数走完立刻回到冻结 没走完的零头丢弃免得下次冻结白送
            if (_isFrozen && _stepTicksToRun <= 0)
            {
                _tickAccumulator = 0;
                return;
            }
        }
    }

    //SectionDirty 方块变更事件写锁外触发 dispatcher 订阅调 MarkDirty 扩散自身+6邻居
    public event Action<SectionPos>? SectionDirty;

    //ChunkUnloaded 区块卸载事件 TryRemove 成功后每 section 触发一次
    //dispatcher 订阅调 UnloadSection 清理 RenderSection 防泄漏
    public event Action<SectionPos>? ChunkUnloaded;

    //EnterReadLock/ExitReadLock 供 SectionRenderDispatcher 持读锁调 Build 防 SetBlockState 数据竞争
    //持读锁期间允许并发读 SetBlockState 持写锁阻塞 RecursionPolicy.NoRecursion 不可递归调用内部加锁的读方法
    public void EnterReadLock() => _syncRoot.EnterReadLock();
    public void ExitReadLock() => _syncRoot.ExitReadLock();

    //LoadChunk 装入区块覆盖同位置旧区块 ConcurrentDictionary 索引器线程安全不加写锁
    public void LoadChunk(ChunkAccess chunk) => _chunks[chunk.Pos] = chunk;

    //LoadLight 装入区段光照覆盖同位置旧光照 ConcurrentDictionary 索引器线程安全
    //区块初次装载时两层一起下发 装完照样要标脏 否则网格用的是没有光照的那份数据
    public void LoadLight(SectionPos pos, DataLayer blockLight, DataLayer skyLight)
    {
        _lights[pos.AsLong()] = (blockLight, skyLight);
        SectionDirty?.Invoke(pos);
    }

    //SetLightLayer 只更新单层光照另一层保留原值 缺失时按空层补齐
    //光照包按层增量下发 天光与方块光各自到齐 不能用 LoadLight 整段覆盖否则先到的一层被清
    //光照是烘焙进区段网格的 写完必须通知渲染层重建 不通知就只能等下一次方块变更才刷出来
    public void SetLightLayer(SectionPos pos, bool skyLayer, DataLayer layer)
    {
        var key = pos.AsLong();
        if (!_lights.TryGetValue(key, out var current))
            current = (new DataLayer(), new DataLayer());
        _lights[key] = skyLayer ? (current.BlockLight, layer) : (layer, current.SkyLight);
        SectionDirty?.Invoke(pos);
    }

    //SetBlockEntityData 记录服务端下发的方块实体状态 空 NBT 表示该位置方块实体已移除
    //移动方块的状态包一并在解析出来 渲染层靠它每帧算推进位移
    public void SetBlockEntityData(BlockPos pos, CompoundTag? tag)
    {
        if (tag is null)
        {
            _blockEntities.TryRemove(pos.AsLong(), out _);
            _movingBlocks.TryRemove(pos.AsLong(), out _);
            return;
        }
        _blockEntities[pos.AsLong()] = tag;
        //有 blockState 段的就是移动方块 空气 id 为 0 不建记录
        if (tag.GetIntOr("blockState", -1) > 0)
            _movingBlocks[pos.AsLong()] = new ClientMovingBlock(pos,
                BlockStateRegistry.GetState(tag.GetIntOr("blockState", 0)),
                Direction.ById(tag.GetIntOr("facing", 0)),
                tag.GetBooleanOr("extending", false),
                tag.GetBooleanOr("source", false));
    }

    //MovingBlocks 客户端移动方块只读视图供渲染层遍历
    public IReadOnlyCollection<ClientMovingBlock> MovingBlocks => _movingBlocks.Values.ToArray();

    //ForgetMovingBlock 该位置不再是移动方块时清掉本地记录
    //服务端把 moving_piston 换成真方块时不会补发空方块实体包 靠方块更新这条路径收尾
    public void ForgetMovingBlock(BlockPos pos) => _movingBlocks.TryRemove(pos.AsLong(), out _);

    //TryGetBlockEntityData 取方块实体状态供渲染层读取
    public bool TryGetBlockEntityData(BlockPos pos, out CompoundTag? tag)
        => _blockEntities.TryGetValue(pos.AsLong(), out tag);

    //AddEntity 记录服务端下发的实体 同 id 覆盖
    //属性表按实体类型取默认表 客户端本地就有初始属性 服务端只在属性被改过时补发
    public void AddEntity(int id, EntityType<object> type, Vec3 pos, float yRot, float xRot, Vec3 velocity, bool onGround)
        => _entities[id] = new ClientEntity(type, pos, yRot, xRot, velocity, onGround)
        {
            Attributes = new AttributeMap(DefaultAttributes.GetSupplier(type) ?? AttributeSupplier.Empty),
            //浮动相位取随机数 对应原版客户端实体构造里的 bobOffs 该值服务端不同步
            BobOffset = Random.Shared.NextSingle() * MathF.PI * 2f,
            SpawnedAtTick = _tickCount,
        };

    //RemoveEntities 移除服务端删除的实体
    public void RemoveEntities(int[] ids)
    {
        foreach (var id in ids) _entities.TryRemove(id, out _);
    }

    //MoveEntity 按相对位移与朝向更新实体 位移单位为 1/4096 格 角度为 null 表示该轴未变
    public void MoveEntity(int id, double dx, double dy, double dz, float? yRot, float? xRot, bool onGround)
    {
        if (!_entities.TryGetValue(id, out var entity)) return;
        _entities[id] = entity with
        {
            Pos = new Vec3(entity.Pos.X + dx, entity.Pos.Y + dy, entity.Pos.Z + dz),
            YRot = yRot ?? entity.YRot,
            XRot = xRot ?? entity.XRot,
            OnGround = onGround,
        };
    }

    //SetEntityPosition 传送包整段覆盖实体位置与朝向
    public void SetEntityPosition(int id, Vec3 pos, float yRot, float xRot, bool onGround)
    {
        if (!_entities.TryGetValue(id, out var entity)) return;
        _entities[id] = entity with { Pos = pos, YRot = yRot, XRot = xRot, OnGround = onGround };
    }

    //SetEntityMotion 记录实体速度供插值使用
    public void SetEntityMotion(int id, Vec3 velocity)
    {
        if (_entities.TryGetValue(id, out var entity))
            _entities[id] = entity with { Velocity = velocity };
    }

    //SetEntityData 更新实体元数据 逐条按索引覆盖
    //字典挂在记录实例上原地改 不重建记录 避免把其它字段的更新冲掉
    public void SetEntityData(int id, IReadOnlyList<EntityDataItem> items)
    {
        if (!_entities.TryGetValue(id, out var entity)) return;
        foreach (var item in items) entity.Data[item.Index] = item.Value;
    }

    //SetEntityAttributes 应用服务端下发的属性快照 对应原版 handleUpdateAttributes
    //每条按原版做法覆盖基值再整体替换修饰符 客户端表是开放表 收到什么属性都能落进来
    public void SetEntityAttributes(int id, IReadOnlyList<AttributeSnapshot> snapshots)
    {
        if (!_entities.TryGetValue(id, out var entity)) return;
        foreach (var snapshot in snapshots)
        {
            var instance = entity.Attributes.GetInstance(snapshot.Attribute);
            if (instance is null) continue;
            instance.SetBaseValue(snapshot.Base);
            foreach (var modifier in instance.Modifiers) instance.RemoveModifier(modifier.Id);
            foreach (var modifier in snapshot.Modifiers) instance.AddTransientModifier(modifier);
        }
    }

    //TryGetEntity 取客户端实体副本
    public bool TryGetEntity(int id, out ClientEntity? entity)
        => _entities.TryGetValue(id, out entity);

    //Entities 客户端实体只读视图供渲染层遍历
    public IReadOnlyCollection<ClientEntity> Entities => _entities.Values.ToArray();

    //UnloadChunk 移除区块及关联光照遍历该 chunk 所有 sectionY
    //ConcurrentDictionary.Keys 快照遍历安全移除线程安全
    //TryRemove 成功后触发 ChunkUnloaded 每 section 一次通知渲染层清理
    public void UnloadChunk(ChunkPos pos)
    {
        if (!_chunks.TryRemove(pos, out var chunk)) return;
        var keysToRemove = new List<long>();
        foreach (var key in _lights.Keys)
        {
            var sx = SectionPos.GetX(key);
            var sz = SectionPos.GetZ(key);
            if (sx == pos.X && sz == pos.Z) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove) _lights.TryRemove(key, out _);
        //同区块的方块实体状态一并清扫 键是压缩后的 BlockPos
        foreach (var key in _blockEntities.Keys)
        {
            if ((BlockPos.GetX(key) >> 4) == pos.X && (BlockPos.GetZ(key) >> 4) == pos.Z)
                _blockEntities.TryRemove(key, out _);
        }
        //事件在移除后触发 回调内 GetSection 已返回 null 不会死锁
        for (var y = chunk.MinSectionY; y <= chunk.MaxSectionY; y++)
            ChunkUnloaded?.Invoke(new SectionPos(pos.X, y, pos.Z));
    }

    //GetBlockState 查世界坐标方块状态越界或未装载返回 default（air）
    //调用方负责持读锁防 SetBlockState 数据竞争 section 内部非线程安全
    public BlockState GetBlockState(BlockPos pos)
    {
        var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return default;
        var section = chunk.GetSection(pos.Y >> 4);
        if (section is null) return default;
        return section.GetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15);
    }

    //GetBlockLight 查世界坐标方块光照越界返回 0
    public int GetBlockLight(BlockPos pos) => GetLight(pos, true);

    //GetSkyLight 查世界坐标天空光照越界返回 15
    public int GetSkyLight(BlockPos pos) => GetLight(pos, false);

    //GetLight 内部光照查询 blockLight=true 取 BlockLight 否则 SkyLight
    //区段越界 block=0 sky=15 对齐原版默认行为
    private int GetLight(BlockPos pos, bool blockLight)
    {
        var sectionPos = SectionPos.Of(pos);
        if (!_lights.TryGetValue(sectionPos.AsLong(), out var light))
            return blockLight ? 0 : 15;
        var layer = blockLight ? light.BlockLight : light.SkyLight;
        return layer.Get(pos.X & 15, pos.Y & 15, pos.Z & 15);
    }

    //GetSection 按区段坐标取 LevelChunkSection 未装载返回 null
    //调用方负责持读锁防 SetBlockState 数据竞争
    public LevelChunkSection? GetSection(int sectionX, int sectionY, int sectionZ)
    {
        var chunkPos = new ChunkPos(sectionX, sectionZ);
        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return null;
        return chunk.GetSection(sectionY);
    }

    //HasChunk 区块是否已装载 ConcurrentDictionary 线程安全
    public bool HasChunk(ChunkPos pos) => _chunks.ContainsKey(pos);

    //GetLoadedChunks 返回所有已装载区块快照供 LevelRenderer/ViewArea 遍历
    //ConcurrentDictionary.Values 返回快照遍历安全调用方不需持锁
    public IEnumerable<ChunkAccess> GetLoadedChunks() => _chunks.Values;

    //SetBlockState 改方块加写锁保护 section 内部 PalettedContainer 写锁外触发 SectionDirty
    //chunk 或 section 未装载返回 false 不触发事件
    public bool SetBlockState(BlockPos pos, BlockState state)
    {
        _syncRoot.EnterWriteLock();
        try
        {
            var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
            if (!_chunks.TryGetValue(chunkPos, out var chunk)) return false;
            var section = chunk.GetSection(pos.Y >> 4);
            if (section is null) return false;
            section.SetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15, state);
        }
        finally { _syncRoot.ExitWriteLock(); }
        //换成别的方块说明这一格的搬运结束了 本地移动方块记录随之作废
        if (state.Owner.Id.Path != "moving_piston") ForgetMovingBlock(pos);
        //写锁外触发事件防回调内读 ClientLevel 死锁 dispatcher.MarkDirty 会扩散自身+6邻居
        SectionDirty?.Invoke(SectionPos.Of(pos));
        return true;
    }

    //ClientEntity 客户端实体状态只保留渲染与插值需要的字段
    //Data 实体元数据按索引存 掉落物的物品栈在 ItemEntity.DataItemIndex
    public sealed record ClientEntity(EntityType<object> Type, Vec3 Pos, float YRot, float XRot, Vec3 Velocity,
        bool OnGround)
    {
        //Data 实体元数据 索引到值的映射
        public Dictionary<byte, object> Data { get; init; } = new();

        //Attributes 实体属性表 按实体类型取默认表 AddEntity 时会覆盖 没指定时是空表
        public AttributeMap Attributes { get; init; } = new(AttributeSupplier.Empty);

        //BobOffset 上下浮动相位 实体加入时随机取 对应原版客户端实体构造里的 bobOffs
        public float BobOffset { get; init; }

        //SpawnedAtTick 客户端收到该实体时的世界刻 与当前刻相减得到存活刻数驱动动画
        //用刻数而不是墙钟是为了世界冻结时动画能一起停住
        public long SpawnedAtTick { get; init; }

        //DroppedItem 掉落物持有的物品栈 非掉落物或尚未收到元数据时为 null
        public ItemStack? DroppedItem
            => Data.TryGetValue(World.Entity.ItemEntity.DataItemIndex, out var value) ? value as ItemStack : null;
    }

    //ClientMovingBlock 客户端一格的移动方块 对应原版客户端那份 PistonMovingBlockEntity
    //服务端只在活塞搬运当刻下发一次状态包 之后的进度由客户端按世界走过的刻数自己推
    //TicksAlive 收到状态包后世界走过了几刻 用计刻而不是记时间戳 免得包到达当帧就被多算半格
    public sealed class ClientMovingBlock
    {
        public ClientMovingBlock(BlockPos pos, BlockState movedState, Direction direction, bool extending,
            bool isSource)
        {
            Pos = pos;
            MovedState = movedState;
            Direction = direction;
            Extending = extending;
            IsSource = isSource;
        }

        public BlockPos Pos { get; }
        public BlockState MovedState { get; }
        public Direction Direction { get; }
        public bool Extending { get; }
        public bool IsSource { get; }

        //TicksAlive 已走过的刻数 推完两刻就够了再多也不影响
        public int TicksAlive { get; private set; }

        //Progress 当前推进进度 0 到 1 每刻半格两刻走完 对应原版 PistonMovingBlockEntity.getProgress
        public float Progress => MathF.Min(1f, TicksAlive * 0.5f);

        //Advance 世界推进一刻时调用
        public void Advance()
        {
            if (TicksAlive < 2) TicksAlive++;
        }

        //RenderState 这一格推进期间真正画出来的样子
        //缩回时活塞那截的 movedState 是活塞底座 照画会看到一个完整活塞方块整块平移回来
        //原版这一格画的是活塞头 与 PistonMovingBlockEntity.getCollisionRelatedBlockState 同一口径
        public BlockState RenderState
        {
            get
            {
                if (Extending || !IsSource) return MovedState;
                if (MovedState.Owner.Id.Path is not ("piston" or "sticky_piston")) return MovedState;
                return NetCraft.Game.World.Level.Block.Blocks.PISTON_HEAD.DefaultBlockState
                    .SetValue(BlockStateProperties.Short, Progress > 0.25f)
                    .SetValue(BlockStateProperties.PistonTypeProperty,
                        MovedState.Owner.Id.Path == "sticky_piston"
                            ? NetCraft.Registry.Enums.PistonType.sticky
                            : NetCraft.Registry.Enums.PistonType.normal)
                    .SetValue(BlockStateProperties.FacingProperty,
                        MovedState.GetValue(BlockStateProperties.FacingProperty));
            }
        }
    }
}
