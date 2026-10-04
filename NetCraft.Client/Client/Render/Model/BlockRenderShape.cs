using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.Client.Render.Model;

//BlockRenderShape 方块渲染形状用于 face culling 判断
//FullBlock 完整立方体可遮挡邻居方块的面
//Empty 空气不渲染不遮挡
//Custom 非完整方块（玻璃/台阶/楼梯）不遮挡邻居面
//首版简化空气=Empty 其他注册方块=FullBlock 后续按模型 element 几何精确判断
public enum BlockRenderShape
{
    Empty,
    FullBlock,
    Custom
}

//BlockRenderShapeProvider 方块渲染形状查询
//按 BlockState.Id 查 Block 判断是否空气
//空气 Empty 其他 FullBlock
//后续可扩展按 BlockBehaviour 查询 shape 或按模型几何判断
public static class BlockRenderShapeProvider
{
    public static BlockRenderShape GetShape(BlockState state)
    {
        var block = BlockStateRegistry.Owner(state.Id);
        //空气方块 Id 为 minecraft:air
        if (block.Id.Path == "air")
            return BlockRenderShape.Empty;
        //移动活塞本体不进区块网格 它的表现由 MovingBlockRenderer 带着进度单独画
        //烘进静态网格的话方块会先闪一下错的形状再跳到终点
        if (block.Id.Path == "moving_piston")
            return BlockRenderShape.Empty;
        return BlockRenderShape.FullBlock;
    }
}
