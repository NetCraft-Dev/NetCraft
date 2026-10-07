using NetCraft.Game.Server;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Updates;

namespace NetCraft.Game.World.Items;

//IUseOnBlockItem an item usable on a block, maps to vanilla Item.useOn
//Only reached when the block's own behavior did not handle it; flint and steel lighting fires goes here and future hoe tilling will use the same path
public interface IUseOnBlockItem
{
    //UseOn returns whether it was handled; when handled the block item placement path is skipped
    bool UseOn(PersistentServerLevel level, ServerPlayer player, ItemStack stack, BlockPos pos, Direction face);
}

//FlintAndSteelItem flint and steel lighting a fire on a block face, maps to vanilla net.minecraft.world.item.FlintAndSteelItem
//Vanilla has a separate branch lighting campfires and candles that depends on the LIT property and block behavior; nc has none of that, so only fire placement is done here
public sealed class FlintAndSteelItem(string name) : Item, IUseOnBlockItem
{
    public override Identifier Id => Identifier.WithDefaultNamespace(name);

    public bool UseOn(PersistentServerLevel level, ServerPlayer player, ItemStack stack, BlockPos pos, Direction face)
    {
        //The fire lands one block outside the clicked face, maps to vanilla relative(clickedFace)
        var target = pos.Offset(face);
        if (!Blocks.BaseFireBlock.CanBePlacedAt(level, target)) return false;
        level.SetBlock(target, Blocks.BaseFireBlock.GetState(level, target), BlockUpdateFlags.All);
        //Vanilla calls hurtAndBreak to damage the item; nc has no durability system, so a non-creative use consumes one
        if (player.GameType != GameType.Creative)
        {
            stack.SetCount(stack.GetCount() - 1);
            player.ContainerMenu?.SendAllDataToRemote();
        }
        return true;
    }
}
