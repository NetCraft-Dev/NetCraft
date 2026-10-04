namespace NetCraft.Game.World.Inventory;

//ContainerData 数值型数据槽对应原版 net.minecraft.world.inventory.ContainerData
//菜单里非物品状态(熔炼进度/切石机选中项)靠它同步给客户端 走 container_set_data 包
public interface ContainerData
{
    //Count 数据项个数
    int Count { get; }

    //Get 读第 index 项
    int Get(int index);

    //Set 写第 index 项
    void Set(int index, int value);
}

//DataSlot 单值数据槽对应原版 net.minecraft.world.inventory.DataSlot
//菜单自己持有值 不需要后端存储
public sealed class DataSlot : ContainerData
{
    private int _value;

    private DataSlot() { }

    //Standalone 建一个独立数据槽
    public static DataSlot Standalone() => new();

    public int Count => 1;

    public int Get(int index) => _value;

    public void Set(int index, int value) => _value = value;
}
