using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//Container 物品容器契约对应原版 net.minecraft.world.Container
//最小集只保留槽位读写与变更通知 打开关闭与玩家距离校验由菜单层负责
public interface Container
{
    //Size 容器槽位数
    int Size { get; }

    //GetItem 取槽位物品 空槽返回 ItemStack.Empty
    ItemStack GetItem(int slot);

    //SetItem 写槽位物品
    void SetItem(int slot, ItemStack stack);

    //RemoveItem 移除指定数量 返回被移除的栈
    ItemStack RemoveItem(int slot, int count);

    //RemoveItemNoUpdate 移除整格 不触发变更通知
    ItemStack RemoveItemNoUpdate(int slot);

    //SetChanged 标记内容已变更 由持有方决定如何通知(菜单同步)
    void SetChanged();

    //IsEmpty 所有槽位是否都为空
    bool IsEmpty();

    //ClearContent 清空所有槽位
    void ClearContent();

    //CanPlaceItem 该槽位是否接受该物品
    bool CanPlaceItem(int slot, ItemStack stack);
}
