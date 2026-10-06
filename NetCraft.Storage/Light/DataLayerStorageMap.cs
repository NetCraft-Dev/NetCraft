using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//DataLayerStorageMap, light layer storage map, maps to vanilla net.minecraft.world.level.lighting.DataLayerStorageMap
//Vanilla holds fastutil's Long2ObjectOpenHashMap with a 2-element lookup hot cache; the cache only saves one hash lookup
//A plain Dictionary is semantically equivalent here; the cache itself is not ported
public abstract class DataLayerStorageMap<TSelf> where TSelf : DataLayerStorageMap<TSelf>
{
    private readonly Dictionary<long, DataLayer> _map;

    protected DataLayerStorageMap() => _map = new Dictionary<long, DataLayer>();

    //Constructor injecting an existing dictionary, reused by subclass Copy
    protected DataLayerStorageMap(Dictionary<long, DataLayer> map) => _map = map;

    //Copy returns a new map of the same type, so the light engine can copy one and modify it incrementally
    public abstract TSelf Copy();

    //CopyDataLayer copies the DataLayer of the given section, writes it back and returns the copy
    public DataLayer CopyDataLayer(long sectionNode)
    {
        var copy = _map[sectionNode].Copy();
        _map[sectionNode] = copy;
        return copy;
    }

    //HasLayer, whether the given section has layer data
    public bool HasLayer(long sectionNode) => _map.ContainsKey(sectionNode);

    //GetLayer returns the given section's layer data, or null when absent
    public DataLayer? GetLayer(long sectionNode)
        => _map.TryGetValue(sectionNode, out var layer) ? layer : null;

    //RemoveLayer removes and returns the given section's layer data
    public DataLayer? RemoveLayer(long sectionNode)
        => _map.Remove(sectionNode, out var layer) ? layer : null;

    //SetLayer writes the given section's layer data
    public void SetLayer(long sectionNode, DataLayer layer) => _map[sectionNode] = layer;

    //Entries exposes all layer data for subclasses to copy
    protected IEnumerable<KeyValuePair<long, DataLayer>> Entries => _map;

    //CopyEntries shallow-copies the backing dictionary for subclass Copy
    protected Dictionary<long, DataLayer> CopyEntries() => new(_map);
}
