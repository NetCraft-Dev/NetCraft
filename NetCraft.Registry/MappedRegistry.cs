using System.Collections.Frozen;
using System.Collections.ObjectModel;
using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Util.Random;

namespace NetCraft.Registry;

//注册表核心实现，维护 6 张映射表：byId/byLocation/byKey/byValue/toId/registrationInfos
//优化点2.3：Freeze后构建FrozenDictionary索引加速只读查询（开关RegistryFrozenDictionary）
public class MappedRegistry<T> : WritableRegistry<T>, HolderOwner<T> where T : class
{
    private readonly ResourceKey<Registry<T>> _key;
    private readonly List<Reference<T>> _byId = new();
    private readonly Dictionary<T, int> _toId = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Identifier, Reference<T>> _byLocation = new();
    private readonly Dictionary<ResourceKey<T>, Reference<T>> _byKey = new();
    private readonly Dictionary<T, Reference<T>> _byValue = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ResourceKey<T>, RegistrationInfo> _registrationInfos = new();
    private readonly Dictionary<TagKey<T>, NamedHolderSet<T>> _allTags = new();
    //侵入式 Holder 缓存按 value 引用查找对应原版 intrusive holders
    private readonly Dictionary<T, Reference<T>> _intrusiveHolders = new(ReferenceEqualityComparer.Instance);
    private Lifecycle _registryLifecycle;
    private bool _frozen;
    //Frozen索引对应优化点2.3 freeze后构建只读查询走FrozenDictionary
    private FrozenDictionary<Identifier, Reference<T>>? _byLocationFrozen;
    private FrozenDictionary<ResourceKey<T>, Reference<T>>? _byKeyFrozen;
    private FrozenDictionary<T, int>? _toIdFrozen;
    private FrozenDictionary<T, Reference<T>>? _byValueFrozen;
    private FrozenDictionary<TagKey<T>, NamedHolderSet<T>>? _allTagsFrozen;

    public MappedRegistry(ResourceKey<Registry<T>> key, Lifecycle lifecycle)
    {
        _key = key;
        _registryLifecycle = lifecycle;
    }

    public ResourceKey<Registry<T>> Key => _key;

    //IsFrozen 是否已冻结 冻结后不能再写入元素
    public bool IsFrozen => _frozen;

    public Lifecycle RegistryLifecycle => _registryLifecycle;

    public override string ToString() => $"Registry[{_key} ({_registryLifecycle})]";

    private void ValidateWrite(ResourceKey<T> key)
    {
        if (_frozen)
            throw new InvalidOperationException($"Registry is already frozen (trying to add key {key})");
    }

    //注册一个值，采用 createStandAlone 模式，value 待 Freeze 时绑定
    //若 value 已通过 CreateIntrusiveHolder 持有 Reference 则复用并 BindKey
    public virtual Reference<T> Register(ResourceKey<T> key, T value, RegistrationInfo registrationInfo)
    {
        //Log.Debug($"Register 入口 key={key} value={value} registrationInfo={registrationInfo}");
        ValidateWrite(key);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        if (_byLocation.ContainsKey(key.Identifier))
            throw new InvalidOperationException($"Adding duplicate key '{key}' to registry");
        if (_byValue.ContainsKey(value))
            throw new InvalidOperationException($"Adding duplicate value '{value}' to registry");

        Reference<T> holder;
        if (_byKey.TryGetValue(key, out var existing))
        {
            holder = existing;
        }
        else if (_intrusiveHolders.TryGetValue(value, out var intrusive))
        {
            holder = intrusive;
            holder.BindKey(key);
        }
        else
        {
            holder = Reference<T>.CreateStandAlone(this, key);
        }
        holder.BindValue(value);

        _byKey[key] = holder;
        _byLocation[key.Identifier] = holder;
        _byValue[value] = holder;
        var newId = _byId.Count;
        _byId.Add(holder);
        _toId[value] = newId;
        _registrationInfos[key] = registrationInfo;
        _registryLifecycle = _registryLifecycle.Add(registrationInfo.Lifecycle);
        //Log.Debug($"Register 出口 result={holder}");
        return holder;
    }

    //CreateIntrusiveHolder 创建侵入式 Holder 对应原版 createIntrusiveHolder
    //value 构造时即持有 Reference 待 Register 时 BindKey 复用
    public Reference<T> CreateIntrusiveHolder(T value)
    {
        //Log.Debug($"CreateIntrusiveHolder 入口 value={value}");
        ArgumentNullException.ThrowIfNull(value);
        if (_intrusiveHolders.TryGetValue(value, out var existing))
        {
            //Log.Debug($"CreateIntrusiveHolder 出口 result={existing}");
            return existing;
        }
        var holder = Reference<T>.CreateIntrusive(this, value);
        _intrusiveHolders[value] = holder;
        //Log.Debug($"CreateIntrusiveHolder 出口 result={holder}");
        return holder;
    }

    public virtual Identifier? GetKey(T thing)
        => TryGetByValue(thing, out var holder) ? holder!.Key.Identifier : null;

    public ResourceKey<T>? GetResourceKey(T thing)
        => TryGetByValue(thing, out var holder) ? holder!.Key : null;

    public virtual int GetId(T thing)
        => TryGetToId(thing, out var id) ? id : IdMap<T>.Default;

    public virtual T? ById(int id)
    {
        if ((uint)id >= (uint)_byId.Count) return null;
        return _byId[id].Value;
    }

    public Reference<T>? Get(int id)
        => (uint)id < (uint)_byId.Count ? _byId[id] : null;

    public Reference<T>? Get(Identifier id)
        => TryGetByLocation(id, out var holder) ? holder : null;

    public virtual Reference<T>? GetAny() => _byId.Count == 0 ? null : _byId[0];

    //GetRandom 按 id 随机返回一个 Holder 空注册表返回 null
    public Holder<T>? GetRandom(RandomSource random)
        => _byId.Count == 0 ? null : _byId[random.NextInt(_byId.Count)];

    public T? GetValue(ResourceKey<T> key)
        => TryGetByKey(key, out var holder) ? holder!.Value : null;

    public virtual T? GetValue(Identifier key)
        => TryGetByLocation(key, out var holder) ? holder!.Value : null;

    public Holder<T> WrapAsHolder(T value)
        => TryGetByValue(value, out var holder) ? holder! : Holder<T>.Direct(value);

    public RegistrationInfo? GetRegistrationInfo(ResourceKey<T> element)
        => _registrationInfos.TryGetValue(element, out var info) ? info : null;

    public int Size => _byKey.Count;

    public bool IsEmpty => _byKey.Count == 0;

    public IReadOnlyCollection<Identifier> KeySet => _byLocation.Keys.ToArray();

    public IReadOnlyCollection<ResourceKey<T>> RegistryKeySet => _byKey.Keys.ToArray();

    public IEnumerable<KeyValuePair<ResourceKey<T>, T>> EntrySet
        => _byKey.Select(e => new KeyValuePair<ResourceKey<T>, T>(e.Key, e.Value.Value));

    public bool ContainsKey(Identifier key)
        => _byLocationFrozen is not null ? _byLocationFrozen.ContainsKey(key) : _byLocation.ContainsKey(key);

    public bool ContainsKey(ResourceKey<T> key)
        => _byKeyFrozen is not null ? _byKeyFrozen.ContainsKey(key) : _byKey.ContainsKey(key);

    //冻结注册表，绑定 value 到 Holder 并校验未绑定项
    //优化点2.3：开关启用时构建FrozenDictionary索引加速后续只读查询
    public Registry<T> Freeze()
    {
        //Log.Debug($"Freeze 入口");
        if (_frozen)
        {
            Log.Debug($"Freeze exit result={this}");
            return this;
        }
        _frozen = true;
        foreach (var (value, holder) in _byValue)
            holder.BindValue(value);
        var unbound = _byKey
            .Where(e => !e.Value.IsBound())
            .Select(e => e.Key.Identifier.ToString())
            .OrderBy(s => s)
            .ToList();
        if (unbound.Count > 0)
            throw new InvalidOperationException($"Unbound values in registry {Key}: [{string.Join(", ", unbound)}]");
        if (OptimizationFlags.RegistryFrozenDictionary)
        {
            //Log.Debug($"步骤1 构建 FrozenDictionary 索引");
            _byLocationFrozen = _byLocation.ToFrozenDictionary();
            _byKeyFrozen = _byKey.ToFrozenDictionary();
            _toIdFrozen = _toId.ToFrozenDictionary();
            _byValueFrozen = _byValue.ToFrozenDictionary();
            _allTagsFrozen = _allTags.ToFrozenDictionary();
        }
        //TODO component：构建 componentLookup
        Log.Debug($"Freeze exit result={this}");
        return this;
    }

    //Frozen查询辅助对应优化点2.3
    //Frozen索引已构建时走FrozenDictionary否则回退Dictionary保证语义一致
    private bool TryGetByLocation(Identifier id, out Reference<T>? holder)
    {
        if (_byLocationFrozen is not null)
        {
            return _byLocationFrozen.TryGetValue(id, out holder);
        }
        return _byLocation.TryGetValue(id, out holder);
    }

    private bool TryGetByKey(ResourceKey<T> key, out Reference<T>? holder)
    {
        if (_byKeyFrozen is not null)
        {
            return _byKeyFrozen.TryGetValue(key, out holder);
        }
        return _byKey.TryGetValue(key, out holder);
    }

    private bool TryGetByValue(T value, out Reference<T>? holder)
    {
        if (_byValueFrozen is not null)
        {
            return _byValueFrozen.TryGetValue(value, out holder);
        }
        return _byValue.TryGetValue(value, out holder);
    }

    private bool TryGetToId(T value, out int id)
    {
        if (_toIdFrozen is not null)
        {
            return _toIdFrozen.TryGetValue(value, out id);
        }
        return _toId.TryGetValue(value, out id);
    }

    //GetOrCreateTagForRegistration按TagKey获取或创建Named HolderSet
    private NamedHolderSet<T> GetOrCreateTagForRegistration(TagKey<T> tag)
    {
        if (!_allTags.TryGetValue(tag, out var named))
        {
            named = new NamedHolderSet<T>(this, tag);
            _allTags[tag] = named;
        }
        return named;
    }

    //BindTags把TagKey到Holder列表映射绑定到Named HolderSet并刷新Reference的tags缓存
    public void BindTags(IReadOnlyDictionary<TagKey<T>, IReadOnlyList<Holder<T>>> pendingTags)
    {
        Log.Debug($"BindTags entry pendingTags={pendingTags}");
        if (!_frozen)
            throw new InvalidOperationException("Registry is not frozen yet, cannot bind tags");

        foreach (var (tag, values) in pendingTags)
        {
            var named = GetOrCreateTagForRegistration(tag);
            named.Bind(values);
        }

        //数据包没有提供文件的标签也要绑成空集 对应原版 tagMap.getOrDefault(key, List.of())
        //留着 unbound 会让引用它的结构在生成时抛 原版语义是没有条目即空集
        foreach (var (_, pending) in _allTags)
        {
            if (!pending.IsBound) pending.Bind(Array.Empty<Holder<T>>());
        }

        var tagsForElement = new Dictionary<Reference<T>, List<TagKey<T>>>(ReferenceEqualityComparer.Instance);
        foreach (var holder in _byValue.Values)
            tagsForElement[holder] = new List<TagKey<T>>();

        foreach (var (tag, named) in _allTags)
        {
            if (!named.IsBound) continue;
            foreach (var holder in named)
            {
                if (holder is Reference<T> reference)
                    tagsForElement[reference].Add(tag);
            }
        }

        foreach (var (reference, tags) in tagsForElement)
            reference.BindTags(tags);

        //BindTags后_allTags变更Frozen索引失效重建
        if (OptimizationFlags.RegistryFrozenDictionary)
        {
            //Log.Debug($"步骤1 重建 _allTagsFrozen 索引");
            _allTagsFrozen = _allTags.ToFrozenDictionary();
        }
        //Log.Debug($"BindTags 出口");
    }

    //Get 按 TagKey 取已绑定的集合
    //tag 查询是方块/物品判定的热路径 每次调用打两条日志会瞬间刷爆日志缓冲 这里刻意不留日志
    public NamedHolderSet<T>? Get(TagKey<T> tag)
    {
        if (_allTagsFrozen is not null)
            return _allTagsFrozen.TryGetValue(tag, out var named) && named.IsBound ? named : null;
        return _allTags.TryGetValue(tag, out var named2) && named2.IsBound ? named2 : null;
    }

    //GetOrCreate 按 TagKey 取或登记实例 对应原版 MappedRegistry.getOrCreateTag
    //Get 只认已绑定的 元素解码期标签还没绑 返回 null 会让调用方自建实例而绑不上
    public NamedHolderSet<T> GetOrCreate(TagKey<T> tag) => GetOrCreateTagForRegistration(tag);

    public IEnumerable<NamedHolderSet<T>> GetTags()
        => _allTags.Values.Where(n => n.IsBound);

    //Holds 是否已绑定该 TagKey 同上热路径不留日志
    public bool Holds(TagKey<T> tag)
    {
        if (_allTagsFrozen is not null)
            return _allTagsFrozen.TryGetValue(tag, out var named) && named.IsBound;
        return _allTags.TryGetValue(tag, out var named2) && named2.IsBound;
    }

    //HolderLookup.ListElements 列举所有已注册 Reference Holder
    public IEnumerable<Holder<T>> ListElements()
        => _byValue.Values.AsEnumerable();

    //HolderLookup.Get 按 ResourceKey 查 Holder 找不到返回 null
    public virtual Holder<T>? Get(ResourceKey<T> key)
        => TryGetByKey(key, out var holder) ? holder! : null;

    //HolderLookup.ListTags 列举所有已绑定标签与对应 HolderSet 复用 GetTags 走 frozen 或 dict 一致
    public IEnumerable<KeyValuePair<TagKey<T>, HolderSet<T>>> ListTags()
        => GetTags().Select(n => new KeyValuePair<TagKey<T>, HolderSet<T>>(n.Key, n));

    //HolderLookup.CanSerializeIn 仅同一注册表实例可序列化
    public bool CanSerializeIn(HolderOwner<T> owner) => ReferenceEquals(this, owner);

    //CreateRegistrationLookup 返回只含当前注册表的 HolderLookupProvider
    //通过 Identifier 匹配后强转 Registry<E> 失败抛 InvalidCastException 对齐原版类型擦除语义
    public HolderLookupProvider CreateRegistrationLookup()
        => new SingleRegistryLookupProvider<T>(this);

    public IEnumerator<T> GetEnumerator() => _byId.Select(h => h.Value).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

//单注册表 HolderLookupProvider 只能查到构造时传入的注册表
//用于 Bootstrap 注册期跨注册表查找场景由上层汇总各注册表 Provider
internal sealed class SingleRegistryLookupProvider<TRegistry> : HolderLookupProvider where TRegistry : class
{
    private readonly Registry<TRegistry> _registry;
    private readonly Identifier _registryId;

    public SingleRegistryLookupProvider(Registry<TRegistry> registry)
    {
        _registry = registry;
        _registryId = registry.Key.Identifier;
    }

    public Registry<T>? Lookup<T>(ResourceKey<Registry<T>> registryKey) where T : class
        => registryKey.Identifier == _registryId ? (Registry<T>)(object)_registry : null;

    public IEnumerable<Identifier> ListRegistryKeys() => new[] { _registryId };
}
