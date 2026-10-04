using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Inventory;
using NetCraft.Network.Chat;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.Block;

//FurnaceBlockEntity 熔炉方块实体 用 smelting 配方 对应原版 FurnaceBlockEntity
public sealed class FurnaceBlockEntity : AbstractFurnaceBlockEntity
{
    public FurnaceBlockEntity(BlockPos pos) : base(BlockEntityTypes.FURNACE, pos) { }

    public override string CookingRecipeType => SmeltingRecipe.SerializerId;

    public override Component DisplayName => Component.Translatable("container.furnace");

    public override AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
        => FurnaceMenu.ForFurnace(containerId, inventory, this);
}

//BlastFurnaceBlockEntity 高炉方块实体 用 blasting 配方 对应原版 BlastFurnaceBlockEntity
public sealed class BlastFurnaceBlockEntity : AbstractFurnaceBlockEntity
{
    public BlastFurnaceBlockEntity(BlockPos pos) : base(BlockEntityTypes.BLAST_FURNACE, pos) { }

    public override string CookingRecipeType => BlastingRecipe.SerializerId;

    public override Component DisplayName => Component.Translatable("container.blast_furnace");

    public override AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
        => FurnaceMenu.ForBlastFurnace(containerId, inventory, this);
}

//SmokerBlockEntity 烟熏炉方块实体 用 smoking 配方 对应原版 SmokerBlockEntity
public sealed class SmokerBlockEntity : AbstractFurnaceBlockEntity
{
    public SmokerBlockEntity(BlockPos pos) : base(BlockEntityTypes.SMOKER, pos) { }

    public override string CookingRecipeType => SmokingRecipe.SerializerId;

    public override Component DisplayName => Component.Translatable("container.smoker");

    public override AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
        => FurnaceMenu.ForSmoker(containerId, inventory, this);
}
