namespace NetCraft.Game.World.Inventory;

//ContainerData numeric data slots, maps to vanilla net.minecraft.world.inventory.ContainerData
//Non-item menu state (smelting progress / stonecutter selection) is synced to the client through it, via the container_set_data packet
public interface ContainerData
{
    //Count number of data entries
    int Count { get; }

    //Get reads entry index
    int Get(int index);

    //Set writes entry index
    void Set(int index, int value);
}

//DataSlot single-value data slot, maps to vanilla net.minecraft.world.inventory.DataSlot
//The menu holds the value itself, no backing store needed
public sealed class DataSlot : ContainerData
{
    private int _value;

    private DataSlot() { }

    //Standalone creates an independent data slot
    public static DataSlot Standalone() => new();

    public int Count => 1;

    public int Get(int index) => _value;

    public void Set(int index, int value) => _value = value;
}
