using NetCraft.Game.Server;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Inventory;

//MenuProvider 菜单提供者对应原版 net.minecraft.world.MenuProvider
//方块实体与其它可交互对象实现它 打开界面时把标题与构造方式交给玩家侧
public interface MenuProvider
{
    //DisplayName 界面标题
    Component DisplayName { get; }

    //CreateMenu 构造菜单 返回 null 表示当前打不开
    AbstractContainerMenu? CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player);
}
