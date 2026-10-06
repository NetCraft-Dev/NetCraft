using NetCraft.Game.Server;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//V-8 smelting blocks; right-click opens the screen, and the burning state is toggled via the lit property by the block entity tick
public static partial class Blocks
{
    public static readonly FurnaceBlock FURNACE = new("furnace");
    public static readonly BlastFurnaceBlock BLAST_FURNACE = new("blast_furnace");
    public static readonly SmokerBlock SMOKER = new("smoker");

    //RegisterFurnaces registers smelting blocks into the real block table
    private static void RegisterFurnaces(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { FURNACE, BLAST_FURNACE, SMOKER };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //FurnaceBlock furnace, maps to vanilla net.minecraft.world.level.block.FurnaceBlock
    public class FurnaceBlock : NamedBlock
    {
        public FurnaceBlock(string name) : base(name) { }

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => new FurnaceBlockEntity(pos);

        //HasBlockEntity furnace has a block entity, pistons cannot push it
        public override bool HasBlockEntity => true;

        //UseOn right-click opens the smelting screen, maps to vanilla useWithoutItem, using the block entity as the menu provider
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (level.GetBlockEntity<FurnaceBlockEntity>(pos) is not { } furnace) return false;
            player.OpenMenu(furnace);
            return true;
        }
    }

    //BlastFurnaceBlock blast furnace, maps to vanilla net.minecraft.world.level.block.BlastFurnaceBlock
    public sealed class BlastFurnaceBlock : FurnaceBlock
    {
        public BlastFurnaceBlock(string name) : base(name) { }

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state)
            => new BlastFurnaceBlockEntity(pos);

        //HasBlockEntity blast furnace has a block entity, pistons cannot push it
        public override bool HasBlockEntity => true;

        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (level.GetBlockEntity<BlastFurnaceBlockEntity>(pos) is not { } furnace) return false;
            player.OpenMenu(furnace);
            return true;
        }
    }

    //SmokerBlock smoker, maps to vanilla net.minecraft.world.level.block.SmokerBlock
    public sealed class SmokerBlock : FurnaceBlock
    {
        public SmokerBlock(string name) : base(name) { }

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => new SmokerBlockEntity(pos);

        //HasBlockEntity smoker has a block entity, pistons cannot push it
        public override bool HasBlockEntity => true;

        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (level.GetBlockEntity<SmokerBlockEntity>(pos) is not { } furnace) return false;
            player.OpenMenu(furnace);
            return true;
        }
    }
}
