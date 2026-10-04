using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//IEntityInsideBehaviour 方块对实体进入的响应 对应原版 BlockBehaviour.entityInside
//实体每 tick 移动后由关卡按包围盒覆盖的方块逐个派发
//玩家不在实体管理器里 关卡派发时会把玩家包围盒一并算进来
public interface IEntityInsideBehaviour
{
    //OnEntityInside 有实体进入该方块 同一刻可能被多个实体各调一次
    void OnEntityInside(ServerLevel level, BlockPos pos, BlockState state);
}
