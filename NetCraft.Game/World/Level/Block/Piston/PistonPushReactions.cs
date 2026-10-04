using NetCraft.Registry.Enums;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.Block.Piston;

//PistonPushReactions 方块的推动反应 对应原版 Blocks.java 里逐条写的 PushReaction
//原版把反应挂在方块属性上 本作由内嵌方块表的 push= 段提供 注册时注入到 BlockBehaviour
//表里没写的就是 normal 与原版 Properties.pushReaction 的默认值一致
internal static class PistonPushReactions
{
    //Of 取该状态对应的推动反应
    public static PushReaction Of(BlockState state) => state.Owner.PushReaction;
}
