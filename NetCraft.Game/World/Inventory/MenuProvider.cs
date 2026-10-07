using NetCraft.Game.Server;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Inventory;

//MenuProvider menu provider, maps to vanilla net.minecraft.world.MenuProvider
//Block entities and other interactable objects implement it, handing the title and construction to the player side when a screen opens
public interface MenuProvider
{
    //DisplayName screen title
    Component DisplayName { get; }

    //CreateMenu builds the menu, null means it cannot be opened right now
    AbstractContainerMenu? CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player);
}
