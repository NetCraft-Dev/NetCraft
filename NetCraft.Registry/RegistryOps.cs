using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//RegistryOps 注册表感知的 DynamicOps 装饰器对应原版 net.minecraft.resources.RegistryOps
//包装底层 DynamicOps<T> 持有 RegistryAccess 提供 Codec 解析时的注册表查询入口
public sealed class RegistryOps<T> : DynamicOps<T>
{
    private readonly DynamicOps<T> _delegate;
    public RegistryAccess RegistryAccess { get; }

    public RegistryOps(DynamicOps<T> delegateOps, RegistryAccess registryAccess)
    {
        _delegate = delegateOps;
        RegistryAccess = registryAccess;
    }

    //GetRegistry 按注册表 key 查注册表返回 null 未找到对应原版 RegistryOps.owner/getter
    //简化点不实现 HolderOwner/HolderGetter 中间层直接返回 Registry<E>
    public Registry<E>? GetRegistry<E>(ResourceKey<Registry<E>> registryKey) where E : class
        => RegistryAccess.Lookup(registryKey);

    //DecodeHolder 按字段名解析 Identifier 后从注册表查 Holder 对应原版 retrieveElement
    //简化点用 IdentifierCodec 解析后查注册表返回 Reference Holder
    public DataResult<Holder<E>> DecodeHolder<E>(ResourceKey<Registry<E>> registryKey, T input) where E : class
    {
        var registry = GetRegistry(registryKey);
        if (registry is null)
            return DataResult<Holder<E>>.Error(() => $"Unknown registry: {registryKey}");
        var idResult = IdentifierCodec.Instance.Parse(this, input);
        if (!idResult.Result().IsPresent)
            return DataResult<Holder<E>>.Error(() => "Failed to decode Identifier");
        var id = idResult.GetOrThrow();
        var value = registry.Get(id);
        if (value is null)
            return DataResult<Holder<E>>.Error(() => $"Can't find value: {id} in registry {registryKey}");
        return DataResult<Holder<E>>.Success(value);
    }

    //EncodeId 把 Holder 编码为 Identifier 字符串对应原版 HOLDER_ID_CODEC 编码路径
    //Reference 用 Key.Identifier Direct 抛异常 Direct 无注册表键
    public DataResult<T> EncodeId<E>(Holder<E> holder) where E : class
    {
        var key = holder.UnwrapKey();
        if (key is null)
            return DataResult<T>.Error(() => "Holder is not a reference");
        return DataResult<T>.Success(_delegate.CreateString(key.Identifier.ToString()));
    }

    public T Empty() => _delegate.Empty();
    public T EmptyList() => _delegate.EmptyList();
    public T EmptyMap() => _delegate.EmptyMap();
    public T CreateByte(byte value) => _delegate.CreateByte(value);
    public T CreateShort(short value) => _delegate.CreateShort(value);
    public T CreateInt(int value) => _delegate.CreateInt(value);
    public T CreateLong(long value) => _delegate.CreateLong(value);
    public T CreateFloat(float value) => _delegate.CreateFloat(value);
    public T CreateDouble(double value) => _delegate.CreateDouble(value);
    public T CreateBoolean(bool value) => _delegate.CreateBoolean(value);
    public T CreateNumeric(double value) => _delegate.CreateNumeric(value);
    public T CreateString(string value) => _delegate.CreateString(value);
    public T CreateList(IEnumerable<T> stream) => _delegate.CreateList(stream);
    public T CreateMap(IEnumerable<Pair<T, T>> map) => _delegate.CreateMap(map);
    public DataResult<double> GetNumberValue(T input) => _delegate.GetNumberValue(input);
    public DataResult<long> GetLongValue(T input) => _delegate.GetLongValue(input);
    public DataResult<string> GetStringValue(T input) => _delegate.GetStringValue(input);
    public DataResult<bool> GetBooleanValue(T input) => _delegate.GetBooleanValue(input);
    public DataResult<T> MergeToList(T list, T value) => _delegate.MergeToList(list, value);
    public DataResult<T> MergeToList(T list, IReadOnlyList<T> values) => _delegate.MergeToList(list, values);
    public DataResult<T> MergeToMap(T map, T key, T value) => _delegate.MergeToMap(map, key, value);
    public DataResult<T> MergeToMap(T map, MapLike<T> values) => _delegate.MergeToMap(map, values);
    public DataResult<T> MergeToMap(T map, IReadOnlyDictionary<T, T> values) => _delegate.MergeToMap(map, values);
    public DataResult<MapLike<T>> GetMap(T input) => _delegate.GetMap(input);
    public DataResult<IEnumerable<Pair<T, T>>> GetMapValues(T input) => _delegate.GetMapValues(input);
    public DataResult<IEnumerable<T>> GetStream(T input) => _delegate.GetStream(input);
    public T Remove(T input, string key) => _delegate.Remove(input, key);
    public U ConvertTo<U>(DynamicOps<U> ops, T input) => _delegate.ConvertTo(ops, input);
    public RecordBuilder<T> MapBuilder() => _delegate.MapBuilder();
}

