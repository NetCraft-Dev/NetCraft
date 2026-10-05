using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Entity;

//IEquipmentHolder 持装备槽的实体 对应原版 LivingEntity.getItemBySlot 那一层能力
//本作没有 LivingEntity 由 Mob 与 Player 各自实现
public interface IEquipmentHolder
{
    //GetItemBySlot 取指定槽位的物品 空槽给空堆
    ItemStack GetItemBySlot(EquipmentSlot slot);
}

//EntityEquipment 装备槽容器 八个槽位按 EquipmentSlot 的 id 索引
public sealed class EntityEquipment
{
    private readonly ItemStack[] _slots = new ItemStack[8];

    public EntityEquipment()
    {
        for (var i = 0; i < _slots.Length; i++) _slots[i] = ItemStack.Empty;
    }

    //Get 取槽位物品 空槽给空堆
    public ItemStack Get(EquipmentSlot slot) => _slots[(int)slot];

    //Set 写入槽位
    public void Set(EquipmentSlot slot, ItemStack stack) => _slots[(int)slot] = stack;
}
