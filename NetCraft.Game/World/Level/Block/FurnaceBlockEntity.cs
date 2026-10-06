using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Inventory;
using NetCraft.Network.Chat;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.Block;

//FurnaceBlockEntity furnace block entity, uses smelting recipes, maps to vanilla FurnaceBlockEntity
public sealed class FurnaceBlockEntity : AbstractFurnaceBlockEntity
{
    public FurnaceBlockEntity(BlockPos pos) : base(BlockEntityTypes.FURNACE, pos) { }

    public override string CookingRecipeType => SmeltingRecipe.SerializerId;

    public override Component DisplayName => Component.Translatable("container.furnace");

    public override AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
        => FurnaceMenu.ForFurnace(containerId, inventory, this);
}

//BlastFurnaceBlockEntity blast furnace block entity, uses blasting recipes, maps to vanilla BlastFurnaceBlockEntity
public sealed class BlastFurnaceBlockEntity : AbstractFurnaceBlockEntity
{
    public BlastFurnaceBlockEntity(BlockPos pos) : base(BlockEntityTypes.BLAST_FURNACE, pos) { }

    public override string CookingRecipeType => BlastingRecipe.SerializerId;

    public override Component DisplayName => Component.Translatable("container.blast_furnace");

    public override AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
        => FurnaceMenu.ForBlastFurnace(containerId, inventory, this);
}

//SmokerBlockEntity smoker block entity, uses smoking recipes, maps to vanilla SmokerBlockEntity
public sealed class SmokerBlockEntity : AbstractFurnaceBlockEntity
{
    public SmokerBlockEntity(BlockPos pos) : base(BlockEntityTypes.SMOKER, pos) { }

    public override string CookingRecipeType => SmokingRecipe.SerializerId;

    public override Component DisplayName => Component.Translatable("container.smoker");

    public override AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
        => FurnaceMenu.ForSmoker(containerId, inventory, this);
}
