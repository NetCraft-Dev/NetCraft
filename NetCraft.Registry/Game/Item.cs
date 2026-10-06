using NetCraft.Codec;

namespace NetCraft.Registry;

//Item abstract base class, maps to vanilla net.minecraft.world.item.Item
//Vanilla holds properties such as ItemCategory/MaxStackSize/DescriptionId
//Simplified to an abstract class here, extended as needed by subclasses; builtInRegistryHolder holds a Reference via CreateIntrusiveHolder
public abstract class Item
{
    //DEFAULT_MAX_STACK_SIZE default stack limit 64
    public const int DEFAULT_MAX_STACK_SIZE = 64;

    //ABSOLUTE_MAX_STACK_SIZE absolute stack limit 99
    public const int ABSOLUTE_MAX_STACK_SIZE = 99;

    //MAX_BAR_WIDTH maximum durability bar width 13
    public const int MAX_BAR_WIDTH = 13;

    //BuiltInRegistryHolder calls CreateIntrusiveHolder at construction to hold a Reference
    //BuiltInRegistries.ITEM reuses it and BindKey on registration
    //BindComponents defaults to Empty at construction; a subclass overriding Components must call BindComponents again itself
    public Reference<Item> BuiltInRegistryHolder { get; }

    protected Item()
    {
        BuiltInRegistryHolder = BuiltInRegistries.ITEM.CreateIntrusiveHolder(this);
        BuiltInRegistryHolder.BindComponents(DataComponentMap.Empty);
    }

    //Id the item's registry name, must be implemented by subclasses
    public abstract Identifier Id { get; }

    //CODEC references an item by registry name, maps to vanilla Item.CODEC
    public static readonly Codec<Holder<Item>> CODEC = new HolderRefCodec<Item>(BuiltInRegistries.ITEM);

    //Components default empty component map; subclasses may override to provide preset components
    public virtual DataComponentMap Components => DataComponentMap.Empty;

    //GetDefaultMaxStackSize default stack limit, overridable by subclasses
    public virtual int GetDefaultMaxStackSize() => DEFAULT_MAX_STACK_SIZE;

    //CraftingRemainder the item left behind when consumed as a container; returns null if none
    //Maps to vanilla Item.getCraftingRemainder; the fuel slot switching a burnt lava bucket back to an empty bucket relies on it
    public virtual Item? CraftingRemainder => null;

    //CanFitInsideContainerItems whether it fits inside container items like bundles, maps to vanilla canFitInsideContainerItems
    public virtual bool CanFitInsideContainerItems => true;
}
