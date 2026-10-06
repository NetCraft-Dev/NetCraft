using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//V-8 crafting blocks; right-clicking the crafting table opens a 3x3 crafting screen
public static partial class Blocks
{
    public static readonly CraftingTableBlock CRAFTING_TABLE = new("crafting_table");

    //RegisterCrafting registers crafting blocks into the real block table
    private static void RegisterCrafting(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { CRAFTING_TABLE };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //CraftingTableBlock crafting table, maps to vanilla net.minecraft.world.level.block.CraftingTableBlock
    //No facing in the block state; the open-screen behavior matches vanilla
    public sealed class CraftingTableBlock : NamedBlock
    {
        public CraftingTableBlock(string name) : base(name) { }

        //UseOn right-click opens the crafting screen, maps to vanilla CraftingTableBlock.useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            player.OpenMenu(new CraftingTableMenuProvider());
            return true;
        }

        //CraftingTableMenuProvider crafting table menu, title uses vanilla container.crafting
        private sealed class CraftingTableMenuProvider : MenuProvider
        {
            public Component DisplayName => Component.Translatable("container.crafting");

            public AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
                => new CraftingMenu(containerId, inventory);
        }
    }
}
