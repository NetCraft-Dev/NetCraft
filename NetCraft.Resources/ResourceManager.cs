using NetCraft.Logging;
using NetCraft.Registry;

namespace NetCraft.Resources;

//ResourceManager, maps to vanilla net.minecraft.server.packs.resources.ResourceManager
//Merges multiple PackResources by priority and provides a unified resource access API
public sealed class ResourceManager
{
    //packs sorted by priority (lower value wins)
    private readonly List<Pack> _packs = new();
    //cache of pack lists grouped by type
    private readonly Dictionary<PackType, List<PackResources>> _byType = new();

    public IReadOnlyList<Pack> Packs => _packs;

    //Reloaded fires after packs are added/removed or an explicit Reload completes
    //ReloadableServerResources subscribes to this event to reload Tags and the like after a pack change
    public event EventHandler<ReloadEventArgs>? Reloaded;

    //AddPack adds a resource pack, inserting it at the right spot by Priority
    public void AddPack(Pack pack)
    {
        var inserted = false;
        for (int i = 0; i < _packs.Count; i++)
        {
            if (_packs[i].Priority > pack.Priority)
            {
                _packs.Insert(i, pack);
                inserted = true;
                break;
            }
        }
        if (!inserted)
        {
            _packs.Add(pack);
        }
        RebuildCache();
        //Read the metadata on acceptance so a malformed pack.mcmeta surfaces here rather than at first use
        try
        {
            if (PackMetadataSectionReader.Read(pack.Resources, PackType.ServerData) is null)
                Log.Debug($"Pack {pack.Id} carries no readable pack.mcmeta");
        }
        catch (Exception e)
        {
            Log.Warning($"Pack {pack.Id} has a malformed pack.mcmeta: {e.Message}");
        }
    }

    //RemovePack removes a resource pack by id
    public bool RemovePack(Identifier id)
    {
        var removed = _packs.RemoveAll(p => p.Id == id) > 0;
        if (removed) RebuildCache();
        return removed;
    }

    //GetResource fetches the highest priority resource for type+location
    public Resource? GetResource(PackType type, Identifier location)
    {
        if (!_byType.TryGetValue(type, out var list)) return null;
        foreach (var pack in list)
        {
            //the probe stream must be released immediately, otherwise folder packs keep the file handle open
            using (var probe = pack.GetResource(type, location))
            {
                if (probe != null)
                {
                    return new Resource(location, pack.PackId, () => pack.GetResource(type, location) ?? new MemoryStream());
                }
            }
        }
        return null;
    }

    //ListResources lists resources matching namespace + pathPrefix across all packs
    public IEnumerable<Resource> ListResources(PackType type, string namespaceName, string pathPrefix)
    {
        if (!_byType.TryGetValue(type, out var list)) yield break;
        var seen = new HashSet<Identifier>();
        foreach (var pack in list)
        {
            pack.ListResources(type, namespaceName, pathPrefix, seen);
        }
        foreach (var id in seen)
        {
            var resource = GetResource(type, id);
            if (resource != null) yield return resource;
        }
    }

    //GetNamespaces returns the union of namespaces across all packs
    public ISet<string> GetNamespaces(PackType type)
    {
        var result = new HashSet<string>();
        if (_byType.TryGetValue(type, out var list))
        {
            foreach (var pack in list)
            {
                result.UnionWith(pack.GetNamespaces(type));
            }
        }
        return result;
    }

    //RebuildCache rebuilds the per-type cache
    private void RebuildCache()
    {
        _byType.Clear();
        foreach (var type in Enum.GetValues<PackType>())
        {
            _byType[type] = _packs.Select(p => p.Resources).ToList();
        }
    }

    //Reload rebuilds the cache and fires the Reloaded event so listeners reload
    //The caller calls this explicitly after AddPack/RemovePack to trigger data-driven reloads such as Tags
    public void Reload()
    {
        RebuildCache();
        Reloaded?.Invoke(this, new ReloadEventArgs(DateTime.UtcNow));
    }
}

//ReloadEventArgs, resource reload event args carrying a timestamp for listener logs
public sealed class ReloadEventArgs : EventArgs
{
    public DateTime Timestamp { get; }
    public ReloadEventArgs(DateTime timestamp) => Timestamp = timestamp;
}
