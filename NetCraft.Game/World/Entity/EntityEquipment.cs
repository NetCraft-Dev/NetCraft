using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Entity;

//IEquipmentHolder entity holding equipment slots, maps to the capability of vanilla LivingEntity.getItemBySlot
//Implemented by LivingEntity; Mob and Player inherit it
public interface IEquipmentHolder
{
    //GetItemBySlot returns the item in the given slot, empty slots give an empty stack
    ItemStack GetItemBySlot(EquipmentSlot slot);
}

//EntityEquipment equipment slot container, the eight slots are indexed by EquipmentSlot id
public sealed class EntityEquipment
{
    private readonly ItemStack[] _slots = new ItemStack[8];

    public EntityEquipment()
    {
        for (var i = 0; i < _slots.Length; i++) _slots[i] = ItemStack.Empty;
    }

    //Get returns the slot item, empty slots give an empty stack
    public ItemStack Get(EquipmentSlot slot) => _slots[(int)slot];

    //Set writes the slot
    public void Set(EquipmentSlot slot, ItemStack stack) => _slots[(int)slot] = stack;
}
