using NetCraft.Codec;

namespace NetCraft.Registry;

//Item 抽象基类对应原版 net.minecraft.world.item.Item
//原版持有 ItemCategory/MaxStackSize/DescriptionId 等属性
//此处简化为抽象类子类按需扩展 builtInRegistryHolder 通过 CreateIntrusiveHolder 持有 Reference
public abstract class Item
{
    //DEFAULT_MAX_STACK_SIZE 默认堆叠上限 64
    public const int DEFAULT_MAX_STACK_SIZE = 64;

    //ABSOLUTE_MAX_STACK_SIZE 绝对堆叠上限 99
    public const int ABSOLUTE_MAX_STACK_SIZE = 99;

    //MAX_BAR_WIDTH 损坏条最大宽度 13
    public const int MAX_BAR_WIDTH = 13;

    //BuiltInRegistryHolder 构造时调 CreateIntrusiveHolder 持有 Reference
    //BuiltInRegistries.ITEM 注册时复用并 BindKey
    //构造时 BindComponents 默认 Empty 子类重写 Components 后需自行重新 BindComponents
    public Reference<Item> BuiltInRegistryHolder { get; }

    protected Item()
    {
        BuiltInRegistryHolder = BuiltInRegistries.ITEM.CreateIntrusiveHolder(this);
        BuiltInRegistryHolder.BindComponents(DataComponentMap.Empty);
    }

    //Id 物品的注册名子类必须实现
    public abstract Identifier Id { get; }

    //Components 默认空组件 map 子类可重写提供预置组件
    public virtual DataComponentMap Components => DataComponentMap.Empty;

    //GetDefaultMaxStackSize 默认堆叠上限子类可重写
    public virtual int GetDefaultMaxStackSize() => DEFAULT_MAX_STACK_SIZE;

    //CraftingRemainder 作为容器类用途消耗后留下的物品 没有返回 null
    //对应原版 Item.getCraftingRemainder 燃料槽烧掉岩浆桶后要换回空桶就是靠它
    public virtual Item? CraftingRemainder => null;

    //CanFitInsideContainerItems 能否塞进收纳袋这类容器物品 对应原版 canFitInsideContainerItems
    public virtual bool CanFitInsideContainerItems => true;
}
