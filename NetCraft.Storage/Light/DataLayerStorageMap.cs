using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//DataLayerStorageMap 光照层存储映射对应原版 net.minecraft.world.level.lighting.DataLayerStorageMap
//原版持 fastutil 的 Long2ObjectOpenHashMap 并带 2 元素查找热点缓存 缓存只是省一次哈希查找
//这里直接用 Dictionary 语义等价 不移植缓存本身
public abstract class DataLayerStorageMap<TSelf> where TSelf : DataLayerStorageMap<TSelf>
{
    private readonly Dictionary<long, DataLayer> _map;

    protected DataLayerStorageMap() => _map = new Dictionary<long, DataLayer>();

    //注入已有字典的构造供子类 Copy 复用
    protected DataLayerStorageMap(Dictionary<long, DataLayer> map) => _map = map;

    //Copy 返回同类型的新映射 供光照引擎复制一份做增量修改
    public abstract TSelf Copy();

    //CopyDataLayer 复制指定 section 的 DataLayer 并写回 返回副本
    public DataLayer CopyDataLayer(long sectionNode)
    {
        var copy = _map[sectionNode].Copy();
        _map[sectionNode] = copy;
        return copy;
    }

    //HasLayer 是否存在指定 section 的层数据
    public bool HasLayer(long sectionNode) => _map.ContainsKey(sectionNode);

    //GetLayer 取指定 section 的层数据 不存在返回 null
    public DataLayer? GetLayer(long sectionNode)
        => _map.TryGetValue(sectionNode, out var layer) ? layer : null;

    //RemoveLayer 移除并返回指定 section 的层数据
    public DataLayer? RemoveLayer(long sectionNode)
        => _map.Remove(sectionNode, out var layer) ? layer : null;

    //SetLayer 写入指定 section 的层数据
    public void SetLayer(long sectionNode, DataLayer layer) => _map[sectionNode] = layer;

    //Entries 暴露全部层数据供子类 Copy 时复制
    protected IEnumerable<KeyValuePair<long, DataLayer>> Entries => _map;

    //CopyEntries 浅复制底层字典供子类 Copy 复用
    protected Dictionary<long, DataLayer> CopyEntries() => new(_map);
}
