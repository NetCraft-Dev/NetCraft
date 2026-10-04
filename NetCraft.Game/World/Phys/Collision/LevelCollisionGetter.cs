using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Phys.Collision;

//LevelCollisionGetter 用碰撞查询口径读服务端关卡的视图
//原版 Level 自己就实现 CollisionGetter 这里关卡在 Storage 层做不了形状 只能另起一层适配
//ServerLevel 只有 BlockPos 版读方块 这里补上 BlockGetter 的三坐标版与高度范围
public sealed class LevelCollisionGetter : CollisionGetter
{
    private readonly ServerLevel _level;
    private readonly int _minSectionY;
    private readonly int _sectionsCount;

    public LevelCollisionGetter(ServerLevel level, int minSectionY, int sectionsCount)
    {
        _level = level;
        _minSectionY = minSectionY;
        _sectionsCount = sectionsCount;
    }

    public int MinSectionY => _minSectionY;

    public int MaxSectionY => _minSectionY + _sectionsCount - 1;

    public int SectionsCount => _sectionsCount;

    //未加载的区块与区段按空气处理 与原版 BlockGetter 一致 否则碰撞会凭空多出方块
    public BlockState GetBlockState(int x, int y, int z)
        => _level.GetBlockState(new BlockPos(x, y, z)) ?? Blocks.AIR.DefaultBlockState;

    //IsUnobstructed 形状放进关卡是否不压到实体 对应原版 EntityGetter.isUnobstructed
    //只算能阻挡建造的实体 掉落物一类不算 已移除的不算
    //玩家不在实体管理器里 由 Game 层注入的玩家包围盒另算
    public bool IsUnobstructed(NetCraft.Registry.Entity? source, VoxelShape shape)
    {
        if (shape.IsEmpty) return true;
        foreach (var entity in _level.EntitiesInBox(shape.Bounds()))
        {
            if (entity.IsRemoved || !entity.BlocksBuilding || ReferenceEquals(entity, source)) continue;
            if (Shapes.JoinIsNotEmpty(shape, Shapes.Create(entity.BoundingBox), BooleanOps.And)) return false;
        }

        if (_level.ExtraEntityBoxes is null) return true;
        foreach (var playerBox in _level.ExtraEntityBoxes())
            if (Shapes.JoinIsNotEmpty(shape, Shapes.Create(playerBox), BooleanOps.And)) return false;
        return true;
    }
}
