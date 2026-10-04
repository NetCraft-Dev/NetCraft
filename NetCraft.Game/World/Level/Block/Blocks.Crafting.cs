using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//V-8 合成类方块 工作台右击打开 3x3 合成界面
public static partial class Blocks
{
    public static readonly CraftingTableBlock CRAFTING_TABLE = new("crafting_table");

    //RegisterCrafting 合成类方块登记进真实方块表
    private static void RegisterCrafting(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { CRAFTING_TABLE };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //CraftingTableBlock 工作台对应原版 net.minecraft.world.level.block.CraftingTableBlock
    //方块状态里没有朝向 打开界面这一条行为与原版一致
    public sealed class CraftingTableBlock : NamedBlock
    {
        public CraftingTableBlock(string name) : base(name) { }

        //UseOn 右击打开合成界面 对应原版 CraftingTableBlock.useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            player.OpenMenu(new CraftingTableMenuProvider());
            return true;
        }

        //CraftingTableMenuProvider 工作台菜单 标题用原版 container.crafting
        private sealed class CraftingTableMenuProvider : MenuProvider
        {
            public Component DisplayName => Component.Translatable("container.crafting");

            public AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
                => new CraftingMenu(containerId, inventory);
        }
    }
}
