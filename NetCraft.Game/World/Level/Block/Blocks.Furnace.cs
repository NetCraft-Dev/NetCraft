using NetCraft.Game.Server;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//V-8 熔炼类方块 右击开界面 燃烧状态由方块实体的 tick 改 lit 属性
public static partial class Blocks
{
    public static readonly FurnaceBlock FURNACE = new("furnace");
    public static readonly BlastFurnaceBlock BLAST_FURNACE = new("blast_furnace");
    public static readonly SmokerBlock SMOKER = new("smoker");

    //RegisterFurnaces 熔炼类方块登记进真实方块表
    private static void RegisterFurnaces(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { FURNACE, BLAST_FURNACE, SMOKER };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //FurnaceBlock 熔炉对应原版 net.minecraft.world.level.block.FurnaceBlock
    public class FurnaceBlock : NamedBlock
    {
        public FurnaceBlock(string name) : base(name) { }

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => new FurnaceBlockEntity(pos);

        //HasBlockEntity 熔炉带方块实体 活塞推不动
        public override bool HasBlockEntity => true;

        //UseOn 右击打开熔炼界面 对应原版 useWithoutItem 拿方块实体当菜单提供者
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (level.GetBlockEntity<FurnaceBlockEntity>(pos) is not { } furnace) return false;
            player.OpenMenu(furnace);
            return true;
        }
    }

    //BlastFurnaceBlock 高炉对应原版 net.minecraft.world.level.block.BlastFurnaceBlock
    public sealed class BlastFurnaceBlock : FurnaceBlock
    {
        public BlastFurnaceBlock(string name) : base(name) { }

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state)
            => new BlastFurnaceBlockEntity(pos);

        //HasBlockEntity 高炉带方块实体 活塞推不动
        public override bool HasBlockEntity => true;

        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (level.GetBlockEntity<BlastFurnaceBlockEntity>(pos) is not { } furnace) return false;
            player.OpenMenu(furnace);
            return true;
        }
    }

    //SmokerBlock 烟熏炉对应原版 net.minecraft.world.level.block.SmokerBlock
    public sealed class SmokerBlock : FurnaceBlock
    {
        public SmokerBlock(string name) : base(name) { }

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => new SmokerBlockEntity(pos);

        //HasBlockEntity 烟熏炉带方块实体 活塞推不动
        public override bool HasBlockEntity => true;

        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (level.GetBlockEntity<SmokerBlockEntity>(pos) is not { } furnace) return false;
            player.OpenMenu(furnace);
            return true;
        }
    }
}
