using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//CommandStorage 命令存储对应原版 net.minecraft.world.level.storage.CommandStorage
///data 的 storage 目标读写的就是它
//按命名空间切成独立的 SavedData 文件 data/<命名空间>/command_storage.dat
//文件内容形如 {"contents":{"路径":标签}} 与原本对齐
public sealed class CommandStorage
{
    //ContainerFileName 容器文件名 与命名空间拼成 SavedData id
    private const string ContainerFileName = "command_storage";

    //ContentsTag 容器内实际内容的包装键 对应原本 Container.CODEC 的 contents 字段
    private const string ContentsTag = "contents";

    private readonly SavedDataStorage _dataStorage;
    private readonly Dictionary<string, Container> _containers = new();

    public CommandStorage(SavedDataStorage dataStorage) => _dataStorage = dataStorage;

    //Get 取指定 id 的内容 不存在返回空复合标签 对应原版 get
    public CompoundTag Get(Identifier id)
    {
        var container = FindContainer(id.Namespace);
        return container is null ? new CompoundTag() : container.Get(id.Path);
    }

    //Set 写入指定 id 的内容 空标签等价删除 对应原版 set
    public void Set(Identifier id, CompoundTag contents)
        => GetOrCreateContainer(id.Namespace).Put(id.Path, contents);

    //Keys 已存在的全部键 供补全与调试查询 对应原版 keys
    public IEnumerable<Identifier> Keys()
    {
        foreach (var (ns, container) in _containers)
            foreach (var path in container.Paths)
                yield return Identifier.FromNamespaceAndPath(ns, path);
    }

    //FindContainer 先查内存再查磁盘 不存在返回 null 不建实例 对应原版 getContainer
    private Container? FindContainer(string ns)
    {
        if (_containers.TryGetValue(ns, out var cached)) return cached;
        var loaded = _dataStorage.GetOrLoad(TypeOf(ns));
        if (loaded is not null) _containers[ns] = loaded;
        return loaded;
    }

    //GetOrCreateContainer 取或新建容器 对应原版 getOrCreateContainer
    private Container GetOrCreateContainer(string ns)
    {
        if (_containers.TryGetValue(ns, out var cached)) return cached;
        var created = _dataStorage.ComputeIfAbsent(TypeOf(ns));
        _containers[ns] = created;
        return created;
    }

    private static SavedDataType<Container> TypeOf(string ns)
        => new ContainerType(Identifier.FromNamespaceAndPath(ns, ContainerFileName));

    //ContainerType 单命名空间容器的数据类型描述符
    private sealed class ContainerType(Identifier id) : SavedDataType<Container>
    {
        public string Id => id.ToString();

        public Container Create(CompoundTag tag, RegistryAccess registryAccess) => Container.Load(id, tag);
    }

    //Container 单个命名空间的存储内容 对应原版 CommandStorage.Container
    internal sealed class Container(string id) : SavedData
    {
        private readonly Dictionary<string, CompoundTag> _storage = new();

        public override string Id { get; } = id;

        public IEnumerable<string> Paths => _storage.Keys;

        //Get 取内容 不存在的键返回空复合标签 对应原版 Container.get
        public CompoundTag Get(string path)
            => _storage.TryGetValue(path, out var tag) ? tag : new CompoundTag();

        //Put 写入内容 空标签等价删除 对应原版 Container.put
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

        //Load 从存档读回内容 非复合值与非复合标签的条目跳过
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
