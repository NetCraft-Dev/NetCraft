using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.Server;

//ServerContainerSynchronizer 服务端容器同步器对应原版 ServerPlayer 的 containerSynchronizer
//把菜单的脏槽/全量内容/数据槽变更落成 Clientbound 容器包发给该玩家
//光标物品走 ClientboundSetCursorItemPacket 槽位物品走 ClientboundContainerSetSlotPacket
public sealed class ServerContainerSynchronizer : ContainerSynchronizer
{
    private readonly ServerPlayer _player;

    public ServerContainerSynchronizer(ServerPlayer player) => _player = player;

    //SendSlotChange 单槽变更 槽号 -1 表示光标物品
    public void SendSlotChange(AbstractContainerMenu menu, int slotIndex, ItemStack stack)
    {
        if (slotIndex == AbstractContainerMenu.CarriedSlotIndex)
            _player.Connection.Send(new ClientboundSetCursorItemPacket(stack));
        else
            _player.Connection.Send(new ClientboundContainerSetSlotPacket(menu.ContainerId, menu.StateId, slotIndex, stack));
    }

    //SendContentUpdate 全量内容 玩家进世界时下发一次
    public void SendContentUpdate(AbstractContainerMenu menu, IReadOnlyList<ItemStack> items, ItemStack carried)
        => _player.Connection.Send(new ClientboundContainerSetContentPacket(
            menu.ContainerId, menu.StateId, new List<ItemStack>(items), carried));

    //SendDataChange 数值型数据槽变更 如熔炉进度
    public void SendDataChange(AbstractContainerMenu menu, int id, int value)
        => _player.Connection.Send(new ClientboundContainerSetDataPacket(menu.ContainerId, id, value));
}
