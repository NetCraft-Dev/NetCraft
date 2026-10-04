using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Block;

//BlockEntityType 方块实体类型对应原版 net.minecraft.world.level.block.entity.BlockEntityType
//每个单例承载三件事: 注册表键(落盘 id 字段) 网络序号(同步包与客户端分派) 实例工厂(放置与读档还原)
//实现 Registry 层的 BlockEntityType<object> 标记接口 注册表按弱类型持有 与实体类型同一套方案
public abstract class BlockEntityType : BlockEntityType<object>
{
    //Id 注册表键 存档写进 id 字段原版按它反查类型
    public abstract Identifier Id { get; }

    //RawId 网络序号 对齐原版 BlockEntityTypes 的静态声明序 客户端按序号分派
    public abstract int RawId { get; }

    //Create 按位置造实例 方块放置与读档还原都走这条
    public abstract BlockEntity Create(BlockPos pos);
}
