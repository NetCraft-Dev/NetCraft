using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.Server;

//ServerContainerSynchronizer server-side container synchronizer, maps to the containerSynchronizer of vanilla ServerPlayer
//Turns the menu's dirty slots/full content/data slot changes into Clientbound container packets sent to the player
//The cursor item goes through ClientboundSetCursorItemPacket, slot items through ClientboundContainerSetSlotPacket
public sealed class ServerContainerSynchronizer : ContainerSynchronizer
{
    private readonly ServerPlayer _player;

    public ServerContainerSynchronizer(ServerPlayer player) => _player = player;

    //SendSlotChange single slot change; slot number -1 means the cursor item
    public void SendSlotChange(AbstractContainerMenu menu, int slotIndex, ItemStack stack)
    {
        if (slotIndex == AbstractContainerMenu.CarriedSlotIndex)
            _player.Connection.Send(new ClientboundSetCursorItemPacket(stack));
        else
            _player.Connection.Send(new ClientboundContainerSetSlotPacket(menu.ContainerId, menu.StateId, slotIndex, stack));
    }

    //SendContentUpdate full content, sent once when the player enters the world
    public void SendContentUpdate(AbstractContainerMenu menu, IReadOnlyList<ItemStack> items, ItemStack carried)
        => _player.Connection.Send(new ClientboundContainerSetContentPacket(
            menu.ContainerId, menu.StateId, new List<ItemStack>(items), carried));

    //SendDataChange numeric data slot change, such as furnace progress
    public void SendDataChange(AbstractContainerMenu menu, int id, int value)
        => _player.Connection.Send(new ClientboundContainerSetDataPacket(menu.ContainerId, id, value));
}
