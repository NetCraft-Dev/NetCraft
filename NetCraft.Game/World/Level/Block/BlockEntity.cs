using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//BlockEntity 方块实体基类对应原版 net.minecraft.world.level.block.entity.BlockEntity
//承载方块自身的可变状态(容器内容/熔炉进度等) 由 BlockEntityManager 统一 tick 与下发
//类型由 BlockEntityType 提供 注册表键用于落盘 网络序号用于同步
public abstract class BlockEntity
{
    protected BlockEntity(BlockEntityType type, BlockPos pos)
    {
        Type = type;
        Pos = pos;
    }

    //Type 方块实体类型 注册表键与网络序号都从它取
    public BlockEntityType Type { get; }

    //TypeId 网络序号 同步包按它标识类型 对应原版 BLOCK_ENTITY_TYPE 注册表 id
    public int TypeId => Type.RawId;

    public BlockPos Pos { get; }

    //Level 所在关卡 由 BlockEntityManager 加入时注入
    public ServerLevel? Level { get; internal set; }

    //Tick 每帧钩子 默认无行为
    public virtual void Tick() { }

    //SaveAdditional 额外状态写入 NBT 默认不写
    public virtual void SaveAdditional(CompoundTag tag) { }

    //LoadAdditional 从 NBT 读回额外状态 默认无行为
    public virtual void LoadAdditional(CompoundTag tag) { }

    //OnRemoved 方块实体被移出世界前的回调 对应原版 preRemoveSideEffects 默认无行为
    public virtual void OnRemoved() { }

    //GetUpdatePacket 生成同步包 每次发送时重新序列化当前状态
    public ClientboundBlockEntityDataPacket GetUpdatePacket()
    {
        var tag = new CompoundTag();
        SaveAdditional(tag);
        return new ClientboundBlockEntityDataPacket(Pos, TypeId, tag);
    }

    //SaveWithFullMetadata 写出含类型与位置的完整 NBT 对应原版 saveWithFullMetadata
    //id 是注册表键 读档与 data get block 都靠它反查类型 顺序与原版一致(id 在 x/y/z 之前)
    public CompoundTag SaveWithFullMetadata()
    {
        var tag = new CompoundTag();
        tag.PutString("id", Type.Id.ToString());
        tag.PutInt("x", Pos.X);
        tag.PutInt("y", Pos.Y);
        tag.PutInt("z", Pos.Z);
        SaveAdditional(tag);
        return tag;
    }

    //LoadCustomOnly 只读回自身状态 位置与类型元数据由关卡维护不参与 对应原版 loadWithComponents
    public void LoadCustomOnly(CompoundTag tag) => LoadAdditional(tag);
}
