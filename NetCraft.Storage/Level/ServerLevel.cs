using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Light;
using NetCraft.Storage.Redstone;
using NetCraft.Storage.Ticks;
using NetCraft.Storage.Updates;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Storage;

//ServerLevel 服务端关卡抽象类对应原版 net.minecraft.world.level.ServerLevel
//持有维度标识与关卡数据访问入口 方块更新链也在这里 它是所有世界变更的唯一入口
//实现 ILevelReader 把谓词层需要的那组读取能力暴露给 Registry 侧
public abstract class ServerLevel : ILevelReader
{
    //懒建 构造期实例还没初始化完不能建
    private INeighborUpdater? _neighborUpdater;

    //Dimension 维度注册名如下界/末地
    public abstract Identifier Dimension { get; }

    //DataVersion 关卡数据版本用于 DataFixer 升级判定
    public abstract int DataVersion { get; }

    //RegistryAccess 注册表访问入口用于 Codec 解析时查表
    //子类提供具体 RegistryAccess 实例对应原版 serverLevel.registryAccess()
    public abstract RegistryAccess RegistryAccess { get; }

    //GetChunk 按 ChunkPos 获取区块访问实例对应原版 getChunk
    //返回 null 表示区块未加载子类提供具体加载逻辑
    public abstract ChunkAccess? GetChunk(ChunkPos pos);

    //GetNextEntityId 分配下一个可用实体 id 对应原版 ServerLevel.getNextEntityId
    //默认不查重 只有持久化关卡知道自己装了哪些实体 子类重写接入占用检查
    public virtual int GetNextEntityId() => NetCraft.Registry.Entity.NextEntityId(_ => false);

    //GameTime 世界总游戏刻
    public long GameTime { get; set; }

    //WorldBorder 世界边界 服务端装配成存档实例 未装配时是默认最大尺寸边界
    public WorldBorder WorldBorder { get; set; } = new();

    //MinBuildHeight 该维度最低可放置高度 默认按原版主世界取值 子类按真实区段覆盖
    public virtual int MinBuildHeight => -64;

    //MaxBuildHeight 该维度最高可放置高度的上界自身不含 默认按原版主世界取值
    public virtual int MaxBuildHeight => 320;

    //ORainLevel 上一刻雨量 与 RainLevel 一起供渲染插值 对应原版 oRainLevel
    public float ORainLevel { get; set; }

    //RainLevel 当前雨量 0-1 每刻按天气目标渐变
    public float RainLevel { get; set; }

    //OThunderLevel 上一刻雷声等级 对应原版 oThunderLevel
    public float OThunderLevel { get; set; }

    //ThunderLevel 当前雷声等级 0-1
    public float ThunderLevel { get; set; }

    //GetRainLevel 按部分刻插值取雨量 对应原版 getRainLevel
    public float GetRainLevel(float deltaPartialTick) => Mth.Lerp(deltaPartialTick, ORainLevel, RainLevel);

    //SetRainLevel 直接设置雨量并把上一刻值对齐 对应原版 setRainLevel
    public void SetRainLevel(float rainLevel)
    {
        var clamped = Mth.Clamp(rainLevel, 0.0f, 1.0f);
        ORainLevel = clamped;
        RainLevel = clamped;
    }

    //GetThunderLevel 雷声等级按雨量缩放 对应原版 getThunderLevel
    public float GetThunderLevel(float deltaPartialTick)
        => Mth.Lerp(deltaPartialTick, OThunderLevel, ThunderLevel) * GetRainLevel(deltaPartialTick);

    //SetThunderLevel 直接设置雷声等级并把上一刻值对齐 对应原版 setThunderLevel
    public void SetThunderLevel(float thunderLevel)
    {
        var clamped = Mth.Clamp(thunderLevel, 0.0f, 1.0f);
        OThunderLevel = clamped;
        ThunderLevel = clamped;
    }

    //CanHaveWeather 该维度是否会有天气 对应原版 canHaveWeather
    //原版判定是无天空光或有天花板或是末地即无天气 本作当前只有主世界这一种能下雨的维度
    public virtual bool CanHaveWeather() => true;

    //IsRaining 是否正在下雨 阈值 0.2 对应原版 isRaining
    public bool IsRaining => CanHaveWeather() && GetRainLevel(1.0f) > 0.2f;

    //IsThundering 是否正在雷暴 阈值 0.9 对应原版 isThundering
    public bool IsThundering => CanHaveWeather() && GetThunderLevel(1.0f) > 0.9;

    //DayTime 昼夜时间 0-23999 循环 旧字段仅命令层过渡使用
    public long DayTime { get; set; }

    //DefaultClock 维度默认时钟 对应原版 DimensionType.defaultClock 由服务器装配
    public Holder<WorldClock>? DefaultClock { get; set; }

    //NeighborUpdater 更新分发器 邻居更新与形状更新两条通道都经它排队
    public INeighborUpdater NeighborUpdater
        => _neighborUpdater ??= new CollectingNeighborUpdater(this, MaxChainedNeighborUpdates);

    //MaxChainedNeighborUpdates 链式更新上限 负数表示不限
    protected virtual int MaxChainedNeighborUpdates => SharedConstants.MaxChainedNeighborUpdates;

    //BlockUpdateSink 更新链的 Game 层副作用出口 未注入时方块实体移除与销毁副作用缺失
    public IBlockUpdateSink? BlockUpdateSink { get; set; }

    //BlockEntityBridge 方块实体的存档与清理桥 未注入时区块落盘不带方块实体
    public IBlockEntityBridge? BlockEntityBridge { get; set; }

    //StructureDataBridge 结构装配结果的存档桥 未注入时区块落盘不带 structures 段
    public IStructureDataBridge? StructureDataBridge { get; set; }

    //Random 关卡随机源 供调度刻与随机刻使用
    public RandomSource Random { get; set; } = RandomSource.Create();

    //IsHandlingTick 是否正处在关卡 tick 的处理流程中 对应原版 ServerLevel.handlingTick
    //由服务端主循环在关卡 tick 开始处置真 方块事件跑完置假
    //活塞收回判定要用它: 同刻内收到收回信号说明搬运还没走完 得降级成丢下
    public bool IsHandlingTick { get; set; }

    //_subTickCount 同一刻内的排入序号 对应原版 Level.subTickCount
    private long _subTickCount;
    private LevelTicks<NetCraft.Registry.Block>? _blockTicks;
    private LevelTicks<NetCraft.Registry.Fluid>? _fluidTicks;
    //_blockEvents 方块事件队列 插入序去重 对应原版 ServerLevel.blockEvents
    private readonly LinkedList<BlockEventData> _blockEvents = new();
    private readonly HashSet<BlockEventData> _blockEventSet = new();
    private readonly List<BlockEventData> _blockEventsToReschedule = new();
    //_tickRegisteredChunks 已登记调度刻容器的区块 避免重复登记
    private readonly HashSet<long> _tickRegisteredChunks = new();

    //BlockTicks 方块调度刻集合 懒建
    public LevelTicks<NetCraft.Registry.Block> BlockTicks
        => _blockTicks ??= new LevelTicks<NetCraft.Registry.Block>(IsPositionTicking);

    //FluidTicks 流体调度刻集合 懒建
    public LevelTicks<NetCraft.Registry.Fluid> FluidTicks
        => _fluidTicks ??= new LevelTicks<NetCraft.Registry.Fluid>(IsPositionTicking);

    //IsPositionTicking 该区块是否在可 tick 范围 默认全部允许 服务端按视距收窄
    protected virtual bool IsPositionTicking(long chunkKey) => true;

    //EnsureChunkTicksRegistered 登记区块的调度刻容器 已登记则什么也不做
    //不能只依赖区块加载回调 区块经 GetChunk 直接取回的路径不会触发那个回调
    //首次登记时才 Unpack 把读档带进来的相对延迟按当前游戏刻换算成绝对刻
    public void EnsureChunkTicksRegistered(ChunkAccess chunk)
    {
        if (!_tickRegisteredChunks.Add(ChunkPos.Pack(chunk.Pos.X, chunk.Pos.Z))) return;
        chunk.BlockTicks.Unpack(GameTime);
        BlockTicks.AddContainer(chunk.Pos, chunk.BlockTicks);
        chunk.FluidTicks.Unpack(GameTime);
        FluidTicks.AddContainer(chunk.Pos, chunk.FluidTicks);
    }

    //EnsureChunkTicksRegistered 按坐标登记 区块取不回来就留到下次再试
    public void EnsureChunkTicksRegistered(ChunkPos pos)
    {
        if (_tickRegisteredChunks.Contains(ChunkPos.Pack(pos.X, pos.Z))) return;
        var chunk = GetChunk(pos);
        if (chunk is not null) EnsureChunkTicksRegistered(chunk);
    }

    //UnregisterChunkTicks 摘掉区块的调度刻容器 对应原版 LevelChunk.unregisterTickContainerFromLevel
    //区块卸载时必须做 容器留在集合里的话该区块再加载会重复登记 排刻也会落在旧容器上
    public void UnregisterChunkTicks(ChunkPos pos)
    {
        if (!_tickRegisteredChunks.Remove(ChunkPos.Pack(pos.X, pos.Z))) return;
        _blockTicks?.RemoveContainer(pos);
        _fluidTicks?.RemoveContainer(pos);
    }

    //GetBlockState 读方块状态 区块或区段未加载返回 null
    public virtual BlockState? GetBlockState(BlockPos pos)
    {
        var chunk = GetChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        return chunk?.GetSection(pos.Y >> 4)?.GetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15);
    }

    //GetLoadedChunk 只取已在内存的区块 不触发加载
    //无区块源的关卡退回普通 GetChunk 它们本来就没有加载这个概念
    public virtual ChunkAccess? GetLoadedChunk(ChunkPos pos) => GetChunk(pos);

    //GetBlockStateIfLoaded 读方块状态且不触发加载 区块不在内存直接给 null
    //每 tick 遍历实体包围盒那类查询必须走它: 位置挨着未加载区块时
    //普通 GetBlockState 会凭空拉起一个没有票的区块 那一拍又被回收 于是区块反复卸载又加载
    //每次卸载都要落盘并清掉区块内的方块实体 活塞这类跨刻中途态方块会连同方块实体一起丢
    public BlockState? GetBlockStateIfLoaded(BlockPos pos)
    {
        var chunk = GetLoadedChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        return chunk?.GetSection(pos.Y >> 4)?.GetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15);
    }

    //IsLoaded 该位置在建造高度内且所在区块已在内存 对应原版 isLoaded
    //原版按区块加载等级判定 本作只认内存里有没有区块 够谓词与命令查询用
    public virtual bool IsLoaded(BlockPos pos)
        => pos.Y >= MinBuildHeight && pos.Y < MaxBuildHeight
            && GetLoadedChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4)) is not null;

    //GetFluidState 该位置的流体状态 取不到方块状态时给空流体 对应原版 getFluidState
    public virtual FluidState GetFluidState(BlockPos pos)
        => GetBlockState(pos)?.FluidState ?? FluidState.Empty;

    //GetMaxLocalRawBrightness 该位置的最大局部亮度 取方块光与天光的较大者 对应原版 getMaxLocalRawBrightness
    public virtual int GetMaxLocalRawBrightness(BlockPos pos)
        => Math.Max(
            GetLightValue(NetCraft.Registry.LightLayer.Block, pos),
            GetLightValue(NetCraft.Registry.LightLayer.Sky, pos));

    //CanSeeSky 该位置能直见天空 天光满 15 即视为可见 对应原版 canSeeSky
    public virtual bool CanSeeSky(BlockPos pos)
        => GetLightValue(NetCraft.Registry.LightLayer.Sky, pos) >= 15;

    //GetBiome 该位置所在区块的生物群系 区块不在内存时给 null 对应原版 getBiome
    public virtual Holder<Biome>? GetBiome(BlockPos pos)
        => GetLoadedChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4))?
            .GetNoiseBiome(pos.X >> 2, pos.Y >> 2, pos.Z >> 2);

    //UpdateLight 方块变化后的光照重算 无光照引擎的关卡为空实现
    public virtual void UpdateLight(BlockPos pos) { }

    //GetLightValue 读指定光照层在该位置的值 无光照引擎的关卡返回 0
    //阳光探测器这类按亮度输出的方块要读真实天光 基类给不出时按全黑处理
    public virtual int GetLightValue(NetCraft.Registry.LightLayer layer, BlockPos pos) => 0;

    //SetBlock 写入方块并完成联动 三参版本补默认传播深度 对应原版同名重载
    public bool SetBlock(BlockPos pos, BlockState state, int updateFlags = BlockUpdateFlags.All)
        => SetBlock(pos, state, updateFlags, BlockUpdateFlags.UpdateLimitDefault);

    //SetBlock 写入方块并完成联动 对应原版 Level.setBlock
    //顺序 写状态 -> 光照 -> 方块实体移除 -> 移除影响 -> onPlace -> 邻居更新 -> 形状更新
    //没有"严格模式提前返回" 816 一类只是让对应判定点落空 方法照样走完
    public bool SetBlock(BlockPos pos, BlockState state, int updateFlags, int updateLimit)
    {
        var previous = WriteBlockState(pos, state);
        if (previous is null || previous.Value == state) return false;
        var oldState = previous.Value;
        var movedByPiston = (updateFlags & BlockUpdateFlags.MoveByPiston) != 0;
        //方块类型变了才算换方块 同方块换状态不算
        var blockChanged = !ReferenceEquals(oldState.Owner, state.Owner);

        //红石元件的变化是排查信号链路的第一现场 记下旧新状态与副作用开关
        if (RedstoneIds.IsRedstoneComponent(oldState.Owner.Id) || RedstoneIds.IsRedstoneComponent(state.Owner.Id))
            Log.Debug($"redstone write {pos} {oldState.Owner.Id}:{oldState.Id} -> {state.Owner.Id}:{state.Id} flags={updateFlags} changed={blockChanged}");

        //标记光照脏点 对应原版 LevelChunk.setBlockState 里的 updateSectionStatus 与 checkBlock
        //这里不能按 hasDifferentLightProperties 过滤: 区段由全空变成有玻璃板这类方块时光照属性没变
        //但区段空态标记必须跟着更新 漏了会让光照引擎一直当它是空的
        //标记只是入队 真正的传播与下发在每刻末尾统一做一次
        UpdateLight(pos);

        if (blockChanged && (updateFlags & BlockUpdateFlags.SkipBlockEntitySideEffects) == 0)
        {
            BlockUpdateSink?.RemoveBlockEntity(pos);
            BlockUpdateSink?.AddBlockEntity(pos, state);
        }

        if (blockChanged && ((updateFlags & BlockUpdateFlags.Neighbours) != 0 || movedByPiston)
            && oldState.Owner is IBlockUpdateBehaviour oldBehaviour)
            oldBehaviour.AffectNeighborsAfterRemoval(this, pos, oldState, movedByPiston);

        if ((updateFlags & BlockUpdateFlags.SkipOnPlace) == 0 && state.Owner is IBlockUpdateBehaviour newBehaviour)
            newBehaviour.OnPlace(this, pos, state, oldState, movedByPiston);

        if ((updateFlags & BlockUpdateFlags.Neighbours) != 0)
            UpdateNeighborsAt(pos, oldState.Owner);

        //形状更新前清掉邻居与抑制掉落两位 对应原版 updateFlags & -34
        if ((updateFlags & BlockUpdateFlags.KnownShape) == 0 && updateLimit > 0)
        {
            var shapeFlags = updateFlags & ~(BlockUpdateFlags.Neighbours | BlockUpdateFlags.SuppressDrops);
            var nextLimit = updateLimit - 1;
            BlockUpdateHelper.UpdateIndirectNeighbourShapes(this, oldState, pos, shapeFlags, nextLimit);
            BlockUpdateHelper.UpdateNeighbourShapes(this, state, pos, shapeFlags, nextLimit);
            BlockUpdateHelper.UpdateIndirectNeighbourShapes(this, state, pos, shapeFlags, nextLimit);
        }

        //客户端同步放在副作用链之后 对应原版 flags 含 UPDATE_CLIENTS 的那一路
        if ((updateFlags & BlockUpdateFlags.Clients) != 0)
            BlockUpdateSink?.BlockChanged(pos, state);

        return true;
    }

    //LevelEvent 广播世界事件 对应原版 Level.levelEvent
    public void LevelEvent(int kind, BlockPos pos, int data)
        => BlockUpdateSink?.LevelEvent(kind, pos, data);

    //GetBlockEntity 取该位置的方块实体 方块实体容器在 Game 层 由副作用出口提供
    public T? GetBlockEntity<T>(BlockPos pos) where T : class
        => BlockUpdateSink?.GetBlockEntity(pos) as T;

    //BlockEntityChanged 方块实体数据变化后同步客户端
    public void BlockEntityChanged(BlockPos pos)
        => BlockUpdateSink?.BlockEntityChanged(pos);

    //SetBlockEntity 放入一个已经建好的方块实体 对应原版 Level.setBlockEntity
    //活塞推动时方块实体要带上被推状态与运动参数 走不了按状态新建那条路
    public void SetBlockEntity(object entity)
        => BlockUpdateSink?.SetBlockEntity(entity);

    //RemoveBlockEntity 移除该位置的方块实体 对应原版 Level.removeBlockEntity
    public void RemoveBlockEntity(BlockPos pos)
        => BlockUpdateSink?.RemoveBlockEntity(pos);

    //PlaySound 在方块位置播放音效 对应原版 Level.playSound 无实体版本
    public void PlaySound(SoundEvent sound, SoundSource source, BlockPos pos, float volume, float pitch)
        => BlockUpdateSink?.PlaySound(sound, source, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, volume, pitch);

    //WriteBlockState 只落状态与高度图不带任何联动 对应原版 LevelChunk.setBlockState 的写入部分
    protected virtual BlockState? WriteBlockState(BlockPos pos, BlockState state)
    {
        var chunk = GetChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        var section = chunk?.GetSection(pos.Y >> 4);
        var previous = section?.SetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15, state);
        if (previous is not null && chunk is not null)
            chunk.UpdateHeightmaps(pos.X, pos.Y, pos.Z, state);
        return previous;
    }

    //UpdateNeighborsAt 向该位置六方向发邻居更新 对应原版 updateNeighborsAt
    public void UpdateNeighborsAt(BlockPos pos, NetCraft.Registry.Block sourceBlock)
        => NeighborUpdater.UpdateNeighborsAtExceptFromFacing(pos, sourceBlock, null);

    //UpdateNeighborsAtExceptFromFacing 跳过指定方向的邻居更新 对应原版同名方法
    public void UpdateNeighborsAtExceptFromFacing(BlockPos pos, NetCraft.Registry.Block sourceBlock,
        Direction? skipDirection)
        => NeighborUpdater.UpdateNeighborsAtExceptFromFacing(pos, sourceBlock, skipDirection);

    //NeighborChanged 单点邻居更新入队 执行时重读该位置状态
    public void NeighborChanged(BlockPos pos, NetCraft.Registry.Block changedBlock)
        => NeighborUpdater.NeighborChanged(pos, changedBlock);

    //NeighborChanged 带状态快照的单点邻居更新 执行时不再重读
    public void NeighborChanged(BlockPos pos, BlockState state, NetCraft.Registry.Block changedBlock,
        bool movedByPiston)
        => NeighborUpdater.NeighborChanged(pos, state, changedBlock, movedByPiston);

    //NeighborShapeChanged 形状更新入队 对应原版 neighborShapeChanged
    //pos 是被更新的方块位置 neighbourPos 是触发这次更新的源方块位置
    public void NeighborShapeChanged(Direction direction, BlockPos pos, BlockPos neighbourPos,
        BlockState neighbourState, int updateFlags, int updateLimit)
        => NeighborUpdater.ShapeUpdate(direction, neighbourState, pos, neighbourPos, updateFlags, updateLimit);

    //ScheduleTick 排入方块调度刻 对应原版 LevelAccessor.scheduleTick
    public void ScheduleTick(BlockPos pos, NetCraft.Registry.Block block, int delay)
        => ScheduleTick(pos, block, delay, TickPriority.Normal);

    public void ScheduleTick(BlockPos pos, NetCraft.Registry.Block block, int delay, TickPriority priority)
    {
        //目标区块可能还没登记容器 就地补上 否则这个刻会被丢掉
        EnsureChunkTicksRegistered(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        BlockTicks.Schedule(new ScheduledTick<NetCraft.Registry.Block>(
            block, pos, GameTime + delay, priority, _subTickCount++));
        //计划刻有没有排上是红石不动作时最需要确认的一环
        if (RedstoneIds.IsRedstoneComponent(block.Id))
            Log.Debug($"redstone schedule {pos} block={block.Id} delay={delay} priority={priority} now={GameTime} due={GameTime + delay}");
    }

    //HasScheduledTick 该位置是否已排了同一方块的调度刻
    public bool HasScheduledTick(BlockPos pos, NetCraft.Registry.Block block)
        => BlockTicks.HasScheduledTick(pos, block);

    //WillTickThisTick 该位置的方块刻是否已在本刻被收集
    public bool WillTickThisTick(BlockPos pos, NetCraft.Registry.Block block)
        => BlockTicks.WillTickThisTick(pos, block);

    //TickBlockTicks 推进方块调度刻 预算对齐原版 ServerLevel.tick 里那个 65536
    public void TickBlockTicks(int maxTicksToProcess = 65536)
        => BlockTicks.Tick(GameTime, maxTicksToProcess, TickBlock);

    //TickBlock 执行一次方块刻 对应原版 ServerLevel.tickBlock
    //位置上的方块必须还是排入时那一个 否则丢弃 原版就靠这个挡掉过期的刻
    private void TickBlock(BlockPos pos, NetCraft.Registry.Block block)
    {
        var state = GetBlockState(pos);
        //过期的刻要打出来 否则只会看到"排了刻但没反应"
        if (state is null || !ReferenceEquals(state.Value.Owner, block))
        {
            if (RedstoneIds.IsRedstoneComponent(block.Id))
                Log.Debug($"redstone dropped expired tick {pos} block={block.Id} owner={state?.Owner.Id.ToString() ?? "chunk not loaded"}");
        }
        if (RedstoneIds.IsRedstoneComponent(block.Id))
            Log.Debug($"redstone run {pos} block={block.Id} state={state.Value.Id} time={GameTime}");
        if (state.Value.Owner is IBlockUpdateBehaviour behaviour)
            behaviour.Tick(this, pos, state.Value, Random);
    }

    //BlockEvent 入队一个方块事件 对应原版 ServerLevel.blockEvent
    public void BlockEvent(BlockPos pos, NetCraft.Registry.Block block, int paramA, int paramB)
    {
        var data = new BlockEventData(pos, block, paramA, paramB);
        if (_blockEventSet.Add(data)) _blockEvents.AddLast(data);
    }

    //FlushBlockUpdates 冲刷本拍积压的方块变化下发 对应原版 chunkSource.tick 里的 broadcastChanges
    public void FlushBlockUpdates() => BlockUpdateSink?.FlushBlockUpdates();

    //RunBlockEvents 执行方块事件队列 对应原版 ServerLevel.runBlockEvents
    //本刻不在可 tick 范围的事件顺延到后续 tick 方块类型不符的直接丢弃
    //事件被方块受理后经副作用出口广播给客户端 包里带的是原版那三个参数
    public void RunBlockEvents()
    {
        _blockEventsToReschedule.Clear();
        while (_blockEvents.First is { } node)
        {
            var data = node.Value;
            _blockEvents.RemoveFirst();
            _blockEventSet.Remove(data);
            if (!IsPositionTicking(ChunkPos.Pack(data.Pos.X >> 4, data.Pos.Z >> 4)))
            {
                _blockEventsToReschedule.Add(data);
                continue;
            }
            var state = GetBlockState(data.Pos);
            if (state is null || !ReferenceEquals(state.Value.Owner, data.Block)) continue;
            if (state.Value.Owner is IBlockUpdateBehaviour behaviour
                && behaviour.TriggerEvent(this, data.Pos, state.Value, data.ParamA, data.ParamB))
                BlockUpdateSink?.BlockEvent(data.Pos, data.Block, data.ParamA, data.ParamB);
        }

        foreach (var data in _blockEventsToReschedule)
            if (_blockEventSet.Add(data)) _blockEvents.AddLast(data);
    }

    //GetSignal 读指定位置对某方向输出的信号 对应原版 SignalGetter.getSignal
    //导体方块会把六向直接信号并进来自身信号 信号源强度与直接信号取最大
    //direction 是从接收者指向被查方块的方向 与原版一致
    public int GetSignal(BlockPos pos, Direction direction)
    {
        var state = GetBlockState(pos);
        if (state is null || state.Value.Owner is not IBlockSignalBehaviour behaviour) return 0;
        var current = state.Value;
        var signal = behaviour.GetSignal(this, pos, current, direction);
        if (behaviour.IsRedstoneConductor(this, pos, current))
            return Math.Max(signal, GetDirectSignalTo(pos));
        return signal;
    }

    //GetDirectSignal 读指定位置的直接信号 对应原版 SignalGetter.getDirectSignal
    public int GetDirectSignal(BlockPos pos, Direction direction)
    {
        var state = GetBlockState(pos);
        if (state is null || state.Value.Owner is not IBlockSignalBehaviour behaviour) return 0;
        return behaviour.GetDirectSignal(this, pos, state.Value, direction);
    }

    //GetDirectSignalTo 六方向直接信号取最大 到 15 提前返回 对应原版 getDirectSignalTo
    //逐级展开的顺序 DOWN UP NORTH SOUTH WEST EAST 与原版一致 不能改成遍历 Values
    public int GetDirectSignalTo(BlockPos pos)
    {
        var signal = Math.Max(0, GetDirectSignal(pos.Offset(Direction.Down), Direction.Down));
        if (signal >= 15) return signal;
        signal = Math.Max(signal, GetDirectSignal(pos.Offset(Direction.Up), Direction.Up));
        if (signal >= 15) return signal;
        signal = Math.Max(signal, GetDirectSignal(pos.Offset(Direction.North), Direction.North));
        if (signal >= 15) return signal;
        signal = Math.Max(signal, GetDirectSignal(pos.Offset(Direction.South), Direction.South));
        if (signal >= 15) return signal;
        signal = Math.Max(signal, GetDirectSignal(pos.Offset(Direction.West), Direction.West));
        if (signal >= 15) return signal;
        return Math.Max(signal, GetDirectSignal(pos.Offset(Direction.East), Direction.East));
    }

    //HasSignal 该位置对某方向是否有信号 对应原版 SignalGetter.hasSignal
    public bool HasSignal(BlockPos pos, Direction direction) => GetSignal(pos, direction) > 0;

    //HasNeighborSignal 六向是否有信号 对应原版 hasNeighborSignal
    public bool HasNeighborSignal(BlockPos pos)
    {
        foreach (var direction in Direction.Values)
            if (GetSignal(pos.Offset(direction), direction) > 0) return true;
        return false;
    }

    //GetBestNeighborSignal 六向信号取最大 到 15 提前返回 对应原版 getBestNeighborSignal
    public int GetBestNeighborSignal(BlockPos pos)
    {
        var best = 0;
        foreach (var direction in Direction.Values)
        {
            var signal = GetSignal(pos.Offset(direction), direction);
            if (signal >= 15) return 15;
            if (signal > best) best = signal;
        }
        return best;
    }

    //GetBestOwnOrNeighbourSignal 自身信号与邻居信号取最大 对应原版 getBestOwnOrNeighbourSignal
    public int GetBestOwnOrNeighbourSignal(BlockPos pos)
    {
        var state = GetBlockState(pos);
        var own = state is not null && state.Value.Owner is IBlockSignalBehaviour { IsSignalSource: true } behaviour
            ? behaviour.OwnSignal(this, pos, state.Value)
            : 0;
        return Math.Max(GetBestNeighborSignal(pos), own);
    }

    //GetControlInputSignal 读控制输入信号 对应原版 SignalGetter.getControlInputSignal
    //中继器的侧向锁定与比较器的侧输入都走它
    //onlyDiodes 为真时只认二极管 原版中继器锁定就靠这条把红石线排除在外
    public int GetControlInputSignal(BlockPos pos, Direction direction, bool onlyDiodes)
    {
        var state = GetBlockState(pos);
        if (state is null || state.Value.Owner is not IBlockSignalBehaviour behaviour) return 0;
        var current = state.Value;
        if (onlyDiodes) return behaviour.IsDiode ? GetDirectSignal(pos, direction) : 0;
        //红石块恒 15 红石线读自身功率 其余信号源读直接信号 对应原版三个分支
        if (current.Owner.Id == RedstoneIds.Block) return 15;
        if (current.Owner.Id == RedstoneIds.Wire)
            return current.HasProperty(BlockStateProperties.Power)
                ? current.GetValue(BlockStateProperties.Power)
                : 0;
        return behaviour.IsSignalSource ? GetDirectSignal(pos, direction) : 0;
    }

    //ExtraEntityBoxes 实体管理器之外的包围盒来源 玩家不在实体管理器里由 Game 层注入
    public Func<IEnumerable<AABB>>? ExtraEntityBoxes { get; set; }

    //EntityBoxes 参与实体进入判定的全部包围盒 子类把实体管理器里的实体接进来
    protected virtual IEnumerable<AABB> EntityBoxes()
        => ExtraEntityBoxes?.Invoke() ?? Enumerable.Empty<AABB>();

    //DispatchEntityInside 派发实体进入方块回调 对应原版 Entity.checkInsideBlocks
    //实体每 tick 移动后调用 按包围盒向内收一点覆盖到的方块逐个派发
    //读方块走不触发加载的版本: 这里每 tick 都跑 碰到未加载的邻居区块不能顺手把它拉起来
    public void DispatchEntityInside()
    {
        foreach (var box in EntityBoxes())
        {
            var shrunk = box.Deflate(1.0E-3);
            var minX = Mth.Floor(shrunk.Min.X);
            var maxX = Mth.Floor(shrunk.Max.X);
            var minY = Mth.Floor(shrunk.Min.Y);
            var maxY = Mth.Floor(shrunk.Max.Y);
            var minZ = Mth.Floor(shrunk.Min.Z);
            var maxZ = Mth.Floor(shrunk.Max.Z);
            for (var x = minX; x <= maxX; x++)
                for (var y = minY; y <= maxY; y++)
                    for (var z = minZ; z <= maxZ; z++)
                    {
                        var pos = new BlockPos(x, y, z);
                        var state = GetBlockStateIfLoaded(pos);
                        if (state?.Owner is IEntityInsideBehaviour behaviour)
                            behaviour.OnEntityInside(this, pos, state.Value);
                    }
        }
    }

    //CountEntitiesInBox 统计盒内的实体数 压力板算信号强度用 对应原版 getEntitiesOfClass 的计数用法
    //玩家由 ExtraEntityBoxes 另算 关卡实体交给子类
    public int CountEntitiesInBox(AABB box)
    {
        var count = 0;
        if (ExtraEntityBoxes is not null)
            foreach (var playerBox in ExtraEntityBoxes())
                if (box.Intersects(playerBox)) count++;
        return count + CountLevelEntitiesInBox(box);
    }

    //CountLevelEntitiesInBox 关卡实体管理器里的实体计数 无实体管理器的关卡为 0
    protected virtual int CountLevelEntitiesInBox(AABB box) => 0;

    //EntitiesInBox 盒内的关卡实体 放置占位检查与将来的实体碰撞共用
    //玩家不在实体管理器里 需要时由调用方另算 ExtraEntityBoxes
    public IEnumerable<NetCraft.Registry.Entity> EntitiesInBox(AABB box) => LevelEntitiesInBox(box);

    //LevelEntitiesInBox 关卡实体管理器里与盒相交的实体 无实体管理器的关卡为空
    protected virtual IEnumerable<NetCraft.Registry.Entity> LevelEntitiesInBox(AABB box)
        => Enumerable.Empty<NetCraft.Registry.Entity>();
}
