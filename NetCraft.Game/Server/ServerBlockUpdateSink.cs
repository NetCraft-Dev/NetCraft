using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Level.Block;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;

namespace NetCraft.Game.Server;

//ServerBlockUpdateSink 更新链的 Game 层副作用实现 服务端装配时挂到关卡上
//方块实体容器与掉落流程都在 Game 层 更新链只在正确时机回调这里
public sealed class ServerBlockUpdateSink : IBlockUpdateSink
{
    private readonly PersistentServerLevel _level;
    private readonly PlayerList _players;
    private readonly BlockEntityManager _blockEntities;

    //本拍被改过、还没下发的方块位置 按位置去重 对应原版每区块的 changedBlocksPerSection
    private readonly HashSet<BlockPos> _pendingBlockUpdates = new();

    public ServerBlockUpdateSink(PersistentServerLevel level, PlayerList players, BlockEntityManager blockEntities)
    {
        _level = level;
        _players = players;
        _blockEntities = blockEntities;
    }

    //RemoveBlockEntity 移除方块实体
    //不额外广播空包: 客户端 handleBlockEntityData 是 getBlockEntity(pos, type).ifPresent(...)
    //type 传 0 会在客户端命中注册表首项那类方块实体 再用空 tag 覆盖它的状态
    //而客户端侧的方块实体移除本来就由随后的方块更新完成(新方块没有方块实体时清掉旧的)
    public bool RemoveBlockEntity(BlockPos pos)
    {
        //移除前给方块实体一次收尾机会 活塞靠它把没走完的动画落地
        if (_blockEntities.Get(pos) is { } entity) entity.OnRemoved();
        return _blockEntities.Remove(pos);
    }

    //DestroyBlock 走完整销毁流程 形状更新算出空气时用 对应原版 Level.destroyBlock
    public void DestroyBlock(BlockPos pos, bool dropItems, int updateLimit)
        => ServerBlockUpdates.DestroyBlock(_level, _players, pos, dropItems);

    //BlockChanged 记下本拍变化的方块 对应原版 flags 含 UPDATE_CLIENTS 时进 ChunkHolder.changedBlocksPerSection
    public void BlockChanged(BlockPos pos, BlockState state) => _pendingBlockUpdates.Add(pos);

    //FlushBlockUpdates 把积压的方块变化发给客户端 对应原版 ChunkHolder.broadcastChanges
    //广播时读当前位置的当前状态 同一格一拍内变多次只发最后那次
    public void FlushBlockUpdates()
    {
        if (_pendingBlockUpdates.Count == 0) return;
        foreach (var pos in _pendingBlockUpdates)
        {
            var state = _level.GetBlockState(pos);
            if (state is not { } current) continue;
            //下发序列是排查客户端表现的第一手依据 少了它只能靠猜
            Log.Debug($"[Send] block update {pos} -> {current.Owner.Id}");
            _players.BroadcastAll(new ClientboundBlockUpdatePacket(pos, current.Id));
        }
        _pendingBlockUpdates.Clear();
    }

    //LevelEvent 广播世界事件 对应原版 Level.levelEvent
    public void LevelEvent(int kind, BlockPos pos, int data)
        => _players.BroadcastAll(new ClientboundLevelEventPacket(kind, pos, data, false));

    //BlockEvent 广播方块事件 对应原版 Level.runBlockEvents 成功触发后的那次广播
    //包里的 blockId 取方块注册表序号 客户端按它找自己的方块再来一遍 triggerEvent
    public void BlockEvent(BlockPos pos, NetCraft.Registry.Block block, int paramA, int paramB)
    {
        var blockId = BuiltInRegistries.BLOCK.GetId(block);
        if (blockId < 0) return;
        //方块事件包是客户端重放活塞搬运的驱动 收不到就只剩最终结果
        Log.Debug($"[Send] block event {pos} {block.Id.Path} b0={paramA} b1={paramB} id={blockId}");
        _players.BroadcastAll(new ClientboundBlockEventPacket(pos, (byte)paramA, (byte)paramB, blockId));
    }

    //AddBlockEntity 为新方块创建方块实体 对应原版 LevelChunk 里的 newBlockEntity
    //没有方块实体的方块直接跳过
    public void AddBlockEntity(BlockPos pos, BlockState state)
    {
        if (state.Owner is not BlockBehaviour behaviour) return;
        if (behaviour.CreateBlockEntity(pos, state) is not { } entity) return;
        _blockEntities.Add(_level, entity);
        _players.BroadcastAll(entity.GetUpdatePacket());
    }

    //GetBlockEntity 取方块实体 比较器那类要读自身可变状态
    public object? GetBlockEntity(BlockPos pos) => _blockEntities.Get(pos);

    //BlockEntityChanged 方块实体数据变化后同步客户端
    public void BlockEntityChanged(BlockPos pos)
    {
        if (_blockEntities.Get(pos) is { } entity) _players.BroadcastAll(entity.GetUpdatePacket());
    }

    //SetBlockEntity 登记已经建好的方块实体并把当前状态发一次给客户端
    public void SetBlockEntity(object entity)
    {
        if (entity is not BlockEntity blockEntity) return;
        _blockEntities.Add(_level, blockEntity);
        //移动活塞的状态就靠这一包过去 客户端据此摆被推方块的模型
        Log.Debug($"[Send] block entity {blockEntity.Pos} {blockEntity.Type.Id}");
        _players.BroadcastAll(blockEntity.GetUpdatePacket());
    }

    //PlaySound 向所有在线玩家播放位置音效
    public void PlaySound(SoundEvent sound, SoundSource source, double x, double y, double z,
        float volume, float pitch)
        => ServerSounds.PlaySound(_players, sound, source, x, y, z, volume, pitch);
}
