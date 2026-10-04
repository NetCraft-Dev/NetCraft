using NetCraft.Logging;

namespace NetCraft.Registry;

//带默认值的注册表实现，找不到的 key/id 回退到 defaultKey 对应的值
public class DefaultedMappedRegistry<T> : MappedRegistry<T>, DefaultedRegistry<T> where T : class
{
    private readonly Identifier _defaultKey;
    private Reference<T>? _defaultValue;

    public DefaultedMappedRegistry(string defaultKey, ResourceKey<Registry<T>> key, Lifecycle lifecycle)
        : base(key, lifecycle)
    {
        _defaultKey = Identifier.Parse(defaultKey);
    }

    //注册时若 key 等于 defaultKey 则缓存默认 Holder
    public override Reference<T> Register(ResourceKey<T> key, T value, RegistrationInfo registrationInfo)
    {
        //Log.Debug($"Register 入口 key={key} value={value} registrationInfo={registrationInfo}");
        var result = base.Register(key, value, registrationInfo);
        if (_defaultKey.Equals(key.Identifier))
            _defaultValue = result;
       // Log.Debug($"Register 出口 result={result}");
        return result;
    }

    public override int GetId(T thing)
    {
        var id = base.GetId(thing);
        return id == IdMap<T>.Default && _defaultValue is not null ? base.GetId(_defaultValue.Value) : id;
    }

    public override Identifier? GetKey(T thing)
        => base.GetKey(thing) ?? _defaultKey;

    public override T? GetValue(Identifier key)
        => base.GetValue(key) ?? _defaultValue?.Value;

    //HolderLookup.Get 按 ResourceKey 查 Holder 未注册回退默认值 Holder 对齐原版 DefaultedRegistry.get
    public override Holder<T>? Get(ResourceKey<T> key)
        => base.Get(key) ?? _defaultValue;

    //原版重写 getOptional 不走 default fallback
    public T? GetOptional(Identifier key) => base.GetValue(key);

    public override Reference<T>? GetAny() => _defaultValue;

    public override T? ById(int id)
        => base.ById(id) ?? _defaultValue?.Value;

    //TODO RandomSource：getRandom 找不到时回退 default

    public Identifier DefaultKey => _defaultKey;
}
