using NetCraft.Game.Server;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Updates;

namespace NetCraft.Game.World.Items;

//IUseOnBlockItem 能对着方块使用的物品 对应原版 Item.useOn
//方块自身行为没处理时才轮到它 打火石点火走这条 将来锄头翻地也走同一条
public interface IUseOnBlockItem
{
    //UseOn 返回是否已处理 处理掉就不再走方块物品的放置流程
    bool UseOn(PersistentServerLevel level, ServerPlayer player, ItemStack stack, BlockPos pos, Direction face);
}

//FlintAndSteelItem 打火石 对着方块面点火 对应原版 net.minecraft.world.item.FlintAndSteelItem
//原版另有一支点燃篝火与蜡烛的分支 依赖 LIT 属性与方块自身行为 nc 暂无 这里只做放火
public sealed class FlintAndSteelItem(string name) : Item, IUseOnBlockItem
{
    public override Identifier Id => Identifier.WithDefaultNamespace(name);

    public bool UseOn(PersistentServerLevel level, ServerPlayer player, ItemStack stack, BlockPos pos, Direction face)
    {
        //火落在点击面外侧一格 对应原版 relative(clickedFace)
        var target = pos.Offset(face);
        if (!Blocks.BaseFireBlock.CanBePlacedAt(level, target)) return false;
        level.SetBlock(target, Blocks.BaseFireBlock.GetState(level, target), BlockUpdateFlags.All);
        //原版走 hurtAndBreak 扣耐久 nc 没有耐久系统 非创造按消耗一个处理
        if (player.GameType != GameType.Creative)
        {
            stack.SetCount(stack.GetCount() - 1);
            player.ContainerMenu?.SendAllDataToRemote();
        }
        return true;
    }
}
