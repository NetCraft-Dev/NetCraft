using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;

namespace NetCraft.Game.Server;

//ServerBlockUpdates 服务端方块变更编排 对应原版 Level.setBlock 的调用方
//完整联动链已下沉到 ServerLevel.SetBlock 这里只管客户端广播与玩家侧表现
//广播范围当前是全部在线玩家 追踪系统接入后收窄到视距内
public static class ServerBlockUpdates
{
    //CenterOfBlock 拿不到命中点时的缺省 按方块中心算
    private static readonly Vec3 CenterOfBlock = new(0.5, 0.5, 0.5);

    //SetBlock 写入方块并按 flags 广播 返回是否真的发生变更
    //strict 对应原版 UPDATE_SKIP_ALL_SIDEEFFECTS 只落状态 客户端要等区块重载才看得见
    //notifyNeighbors 为假时不触发邻居更新但仍同步客户端 对应原版只要 UPDATE_CLIENTS
    //广播本身由关卡副作用出口统一发 见 ServerBlockUpdateSink.BlockChanged
    public static bool SetBlock(PersistentServerLevel level, PlayerList players, BlockPos pos,
        BlockState state, bool notifyNeighbors = true, bool strict = false)
    {
        var flags = strict
            ? BlockUpdateFlags.SkipAllSideEffects
            : notifyNeighbors
                ? BlockUpdateFlags.All
                : BlockUpdateFlags.Clients;
        return level.SetBlock(pos, state, flags);
    }

    //ParticleBlockBreak 破坏方块的世界事件 id 对应原版 LevelEvent.PARTICLE_BLOCK_BREAK
    //客户端按包里的方块状态 id 播破坏粒子与方块自身的破坏音效 不需要 sound_event 注册表
    public const int ParticleBlockBreak = 2001;

    //BreakBlock 玩家破坏方块 依次走破坏表现 -> 掉落 -> 破坏回调 -> 置空气
    //player 可为 null 表示非玩家因素破坏 此时不触发 PlayerDestroy
    public static bool BreakBlock(PersistentServerLevel level, PlayerList players, ServerPlayer? player, BlockPos pos)
    {
        var state = level.GetBlockState(pos);
        //空位置不破坏 也避免对空气触发行为
        if (state is null || state.Value.Owner.IsAir) return false;

        //破坏表现先发再改方块 发的是被破坏方块的状态 id 客户端据此出粒子和挖掘声
        //缺这一步方块在别人眼里是凭空消失 挖掘也没有音效 对应原版 Level.destroyBlock 的 levelEvent
        //破坏者本人要排除掉 他客户端挖掘时本地已经播过一遍 再收一颗就成两声了
        var breakEvent = new ClientboundLevelEventPacket(ParticleBlockBreak, pos, state.Value.Id, false);
        if (player is null) players.BroadcastAll(breakEvent);
        else players.BroadcastAllExcept(player, breakEvent);

        //创造模式破坏不掉落 对应原版 ServerPlayerGameMode.removeBlock 的 canDrop 分支
        var dropItems = player is null || player.GameType != NetCraft.Game.World.Level.GameType.Creative;
        var behaviour = state.Value.Owner as BlockBehaviour;
        if (behaviour is not null)
        {
            //原版顺序 playerWillDestroy 排在移除与掉落之前 多格方块在这里把另一半无掉落解掉
            if (player is not null) behaviour.PlayerWillDestroy(level, player, pos, state.Value);
            if (dropItems)
            {
                foreach (var drop in behaviour.GetDrops(level, player, pos, state.Value))
                    SpawnDrop(level, pos, drop);
            }
            if (player is not null) behaviour.PlayerDestroy(level, player, pos, state.Value);
        }
        var changed = SetBlock(level, players, pos, Blocks.AIR.DefaultBlockState);
        //destroy 排在置空之后 只有真的换掉了才调
        //移动活塞靠它把 FACING 反方向那格一起收掉 玩家挖掉搬运中的方块时底座才不至于卡在伸出态
        if (changed && behaviour is not null) behaviour.Destroy(level, pos, state.Value);
        return changed;
    }

    //DestroyBlock 非玩家因素销毁方块 对应原版 Level.destroyBlock 的掉落与置空部分
    //形状更新算出空气时由更新链的副作用出口调到这里
    public static bool DestroyBlock(PersistentServerLevel level, PlayerList players, BlockPos pos, bool dropItems)
    {
        var state = level.GetBlockState(pos);
        if (state is null || state.Value.Owner.IsAir) return false;
        if (dropItems && state.Value.Owner is BlockBehaviour behaviour)
        {
            foreach (var drop in behaviour.GetDrops(level, null, pos, state.Value))
                SpawnDrop(level, pos, drop);
        }
        return SetBlock(level, players, pos, Blocks.AIR.DefaultBlockState);
    }

    //SpawnDrop 生成方块破坏后的掉落物实体 对应原版 Block.popResource
    //落点是方块中心带 ±0.25 抖动再抬到方块半高减去物品自身半高 初始速度由实体构造给
    //拾取冷却 10 刻 免得刚挖下来就被自己吸回背包
    //篝火烤出成品也走它 掉落表现与挖方块一致
    public static void SpawnDrop(PersistentServerLevel level, BlockPos pos, ItemStack stack)
    {
        if (stack.IsEmpty()) return;
        var halfHeight = EntityTypes.ITEM.Height / 2.0;
        var drop = new ItemEntity(EntityTypes.ITEM,
            pos.X + 0.5 + Random.Shared.NextDouble() * 0.5 - 0.25,
            pos.Y + 0.5 + Random.Shared.NextDouble() * 0.5 - 0.25 - halfHeight,
            pos.Z + 0.5 + Random.Shared.NextDouble() * 0.5 - 0.25,
            stack);
        drop.SetDefaultPickUpDelay();
        level.AddEntity(drop);
    }

    //UseOn 玩家对区块使用 交给方块自身行为处理 返回是否已处理
    public static bool UseOn(PersistentServerLevel level, ServerPlayer player, BlockPos pos, Direction face)
    {
        var state = level.GetBlockState(pos);
        return state is not null
            && state.Value.Owner is BlockBehaviour behaviour
            && behaviour.UseOn(level, player, pos, state.Value, face);
    }

    //UseItemOn 玩家手持物品对方块使用 对应原版 ItemStack.useOn
    //原版顺序是方块自身行为在前物品在后 方块没处理才轮到它 打火石点火走这条
    public static bool UseItemOn(PersistentServerLevel level, ServerPlayer player, BlockPos pos, Direction face,
        InteractionHand hand)
    {
        var held = hand == InteractionHand.OffHand
            ? player.Inventory.GetItem(PlayerInventory.OffhandSlot)
            : player.Inventory.GetSelectedItem();
        return !held.IsEmpty() && held.GetItem() is IUseOnBlockItem item
            && item.UseOn(level, player, held, pos, face);
    }

    //PlaceHeldBlock 玩家手持方块物品对方块面使用时放置方块
    //对应原版 ItemStack.useOn -> BlockItem.place 链 落位为物品关联方块的默认状态
    //手持取客户端上报的交互手 副手有方块而主手空时也应能放置
    //目标位置必须可被替换 冒险/旁观模式拒绝放置 非创造放置成功后消耗一个物品
    public static bool PlaceHeldBlock(PersistentServerLevel level, PlayerList players, ServerPlayer player,
        BlockPos pos, Direction face, InteractionHand hand, Vec3? hitLocal = null)
    {
        var gameType = player.GameType;
        if (gameType.IsBlockPlacingRestricted) return false;

        var held = hand == InteractionHand.OffHand
            ? player.Inventory.GetItem(PlayerInventory.OffhandSlot)
            : player.Inventory.GetSelectedItem();
        if (held.IsEmpty() || held.GetItem() is not BlockItem blockItem) return false;

        var placePos = pos.Offset(face);
        var target = level.GetBlockState(placePos);
        if (target?.Owner is not BlockBehaviour targetBehaviour || !targetBehaviour.CanBeReplaced)
            return false;

        //点到水平侧面时落贴墙变体 对应原版 StandingAndWallBlockItem.getStateForPlacement
        var placedBlock = face.IsHorizontal && blockItem.WallBlock is { } wall
            ? wall
            : blockItem.PlacedBlock;
        //朝向与附着面由方块自己按上下文算 拉杆与火把那类靠它落正确状态
        //观察者要六向所以多给一个玩家视线方向 对应原版 getNearestLookingDirection
        var placedState = placedBlock is BlockBehaviour placedBehaviour
            ? placedBehaviour.GetStateForPlacement(level, placePos, face, Direction.FromYRot(player.Yaw),
                player.GetNearestLookingDirection(), hitLocal ?? CenterOfBlock)
            : placedBlock.DefaultBlockState;
        if (placedState is not { } state) return false;
        //站不住的落位直接拒绝 对应原版 BlockItem.place 里的 canSurvive 检查
        var placementBehaviour = state.Owner as BlockBehaviour;
        if (placementBehaviour is not null && !placementBehaviour.CanSurvive(level, placePos, state))
            return false;

        //形状占位检查 新方块压到实体上时放置失败 对应原版 BlockItem.canPlace 里的 isUnobstructed
        //玩家也算阻挡(原版这里传 null 不排除放置者) 点击位置通常不与放置者自身相交
        CollisionGetter collisionView = new LevelCollisionGetter(level, level.MinSectionY, level.SectionsCount);
        if (!collisionView.IsUnobstructed(state, placePos, CollisionContext.PlacementContext(null)))
            return false;

        if (!SetBlock(level, players, placePos, state)) return false;
        //放置完成后回调 中继器要靠它排首刻 对应原版 Block.setPlacedBy
        placementBehaviour?.SetPlacedBy(level, placePos, state, player);

        //原版扣减判断是 abilities.instabuild 创造模式不消耗
        if (gameType != NetCraft.Game.World.Level.GameType.Creative)
        {
            held.SetCount(held.GetCount() - 1);
            player.ContainerMenu?.SendAllDataToRemote();
        }
        return true;
    }

    //NotifyNeighbors 向该位置六方向发邻居更新 对应原版 updateNeighboursOnBlockSet 的邻居部分
    //批量变更统一刷时用 单格变更已由 ServerLevel.SetBlock 内部完成
    internal static void NotifyNeighbors(PersistentServerLevel level, BlockPos pos, BlockState state)
        => level.UpdateNeighborsAt(pos, state.Owner);
}

//BlockChangeBatch 批量方块变更 对应原版 fill 用 updateFlags 抑制中间副作用再统一刷的流程
//原版每格 setBlock(flags=2|256) 只写状态并标记所属段变化 邻居更新被 flags 关掉
//方块包由 ServerChunkCache 在 tick 末尾按段合并成 section_blocks_update 一次发出
//逐格走 SetBlock 时每格都要发一个 block_update 且各跑一轮光照传播 大区域填充是 O(格数) 轮全量传播
public sealed class BlockChangeBatch
{
    private readonly PersistentServerLevel _level;
    //_states 变更位置到最终状态 同一位置被写多次只留最后一次
    private readonly Dictionary<BlockPos, BlockState> _states = new();

    public BlockChangeBatch(PersistentServerLevel level) => _level = level;

    //Apply 写入一格 返回是否真的发生变更 期间不发包不通知邻居
    //flags 只留跳过方块实体副作用 客户端同步由 Flush 按段合并发 这里带上会退化成逐格发包
    public bool Apply(BlockPos pos, BlockState state)
    {
        if (!_level.SetBlock(pos, state, BlockUpdateFlags.SkipBlockEntitySideEffects))
            return false;
        _states[pos] = state;
        return true;
    }

    //Flush 统一刷光照 方块包与邻居通知 对应原版命令层的 updateNeighboursOnBlockSet
    //sideEffects 为 false 时对应原版 UPDATE_SKIP_ALL_SIDEEFFECTS 只落状态 客户端要等区块重载才可见
    public void Flush(PlayerList players, bool sideEffects = true)
    {
        if (_states.Count == 0) return;
        if (!sideEffects)
        {
            _states.Clear();
            return;
        }
        var positions = new List<BlockPos>(_states.Keys);
        _level.UpdateLightBatch(positions);
        BroadcastBySection(players);
        foreach (var (pos, state) in _states) ServerBlockUpdates.NotifyNeighbors(_level, pos, state);
        _states.Clear();
    }

    //BroadcastBySection 按所属段合并成 section_blocks_update 对应原版 ChunkHolder.broadcastChanges
    //同一段内的变化打成一个包 大区域填充时包数从 O(格数) 降到 O(段数)
    private void BroadcastBySection(PlayerList players)
    {
        var sections = new Dictionary<SectionPos, List<long>>();
        foreach (var (pos, state) in _states)
        {
            var section = SectionPos.Of(pos);
            if (!sections.TryGetValue(section, out var packed))
                sections[section] = packed = new List<long>();
            //每项 (状态id << 12) | 段内 12 位偏移 与原版 ClientboundSectionBlocksUpdatePacket 一致
            packed.Add(((long)state.Id << 12) | (ushort)SectionPos.SectionRelativePos(pos));
        }
        foreach (var (section, packed) in sections)
            players.BroadcastAll(new ClientboundSectionBlocksUpdatePacket(section, packed.ToArray()));
    }
}
