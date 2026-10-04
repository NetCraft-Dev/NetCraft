using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//ContainerSynchronizer 菜单到客户端的同步通道对应原版 net.minecraft.world.inventory.ContainerSynchronizer
//服务端实现直接发包 测试实现记录调用 菜单只依赖该接口不依赖网络
public interface ContainerSynchronizer
{
    //SendSlotChange 单槽变更
    void SendSlotChange(AbstractContainerMenu menu, int slotIndex, ItemStack stack);

    //SendContentUpdate 全量内容同步(进世界初始同步与全量刷新)
    void SendContentUpdate(AbstractContainerMenu menu, IReadOnlyList<ItemStack> items, ItemStack carried);

    //SendDataChange 容器数据变化(熔炉进度/酿造时间等) 最小实现暂无产生点
    void SendDataChange(AbstractContainerMenu menu, int id, int value);
}
