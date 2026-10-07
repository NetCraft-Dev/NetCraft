using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//CommandStorage command storage, maps to vanilla net.minecraft.world.level.storage.CommandStorage
//What /data's storage target reads and writes
//Split into separate SavedData files by namespace: data/<namespace>/command_storage.dat
//The file content looks like {"contents":{"path":tag}}, aligned with vanilla
public sealed class CommandStorage
{
    //ContainerFileName the container file name, joined with the namespace into the SavedData id
    private const string ContainerFileName = "command_storage";

    //ContentsTag the wrapper key for the container's actual content, maps to the contents field of vanilla Container.CODEC
    private const string ContentsTag = "contents";

    private readonly SavedDataStorage _dataStorage;
    private readonly Dictionary<string, Container> _containers = new();

    public CommandStorage(SavedDataStorage dataStorage) => _dataStorage = dataStorage;

    //Get takes the content of the given id; a missing one returns an empty compound tag, maps to vanilla get
    public CompoundTag Get(Identifier id)
    {
        var container = FindContainer(id.Namespace);
        return container is null ? new CompoundTag() : container.Get(id.Path);
    }

    //Set writes the content of the given id; an empty tag is equivalent to deletion, maps to vanilla set
    public void Set(Identifier id, CompoundTag contents)
        => GetOrCreateContainer(id.Namespace).Put(id.Path, contents);

    //Keys all existing keys, for suggestions and debug queries, maps to vanilla keys
    public IEnumerable<Identifier> Keys()
    {
        foreach (var (ns, container) in _containers)
            foreach (var path in container.Paths)
                yield return Identifier.FromNamespaceAndPath(ns, path);
    }

    //FindContainer checks memory then disk; a missing one returns null without creating an instance, maps to vanilla getContainer
    private Container? FindContainer(string ns)
    {
        if (_containers.TryGetValue(ns, out var cached)) return cached;
        var loaded = _dataStorage.GetOrLoad(TypeOf(ns));
        if (loaded is not null) _containers[ns] = loaded;
        return loaded;
    }

    //GetOrCreateContainer gets or creates a container, maps to vanilla getOrCreateContainer
    private Container GetOrCreateContainer(string ns)
    {
        if (_containers.TryGetValue(ns, out var cached)) return cached;
        var created = _dataStorage.ComputeIfAbsent(TypeOf(ns));
        _containers[ns] = created;
        return created;
    }

    private static SavedDataType<Container> TypeOf(string ns)
        => new ContainerType(Identifier.FromNamespaceAndPath(ns, ContainerFileName));

    //ContainerType the data type descriptor for a single-namespace container
    private sealed class ContainerType(Identifier id) : SavedDataType<Container>
    {
        public string Id => id.ToString();

        public Container Create(CompoundTag tag, RegistryAccess registryAccess) => Container.Load(id, tag);
    }

    //Container the storage content of a single namespace, maps to vanilla CommandStorage.Container
    internal sealed class Container(string id) : SavedData
    {
        private readonly Dictionary<string, CompoundTag> _storage = new();

        public override string Id { get; } = id;

        public IEnumerable<string> Paths => _storage.Keys;

        //Get takes content; a missing key returns an empty compound tag, maps to vanilla Container.get
        public CompoundTag Get(string path)
            => _storage.TryGetValue(path, out var tag) ? tag : new CompoundTag();

        //Put writes content; an empty tag is equivalent to deletion, maps to vanilla Container.put
        public void Put(string path, CompoundTag contents)
        {
            if (contents.Count == 0) _storage.Remove(path);
            else _storage[path] = contents;
            SetDirty();
        }

        public override CompoundTag Save(CompoundTag tag)
        {
            var contents = new CompoundTag();
            foreach (var (path, value) in _storage) contents.Put(path, value);
            tag.Put(ContentsTag, contents);
            return tag;
        }

        //Load reads the content back from the save; entries that are not compound values/tags are skipped
        public static Container Load(Identifier id, CompoundTag tag)
        {
            var container = new Container(id.ToString());
            if (tag.GetCompound(ContentsTag) is not { } contents) return container;
            foreach (var (path, value) in contents)
                if (value is CompoundTag compound && compound.Count > 0)
                    container._storage[path] = compound;
            return container;
        }
    }
}
