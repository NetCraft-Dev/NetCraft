using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//RegistryOps registry-aware DynamicOps decorator, maps to vanilla net.minecraft.resources.RegistryOps
//Wraps an underlying DynamicOps<T> and holds a RegistryAccess to provide the registry lookup entry point during Codec resolution
public sealed class RegistryOps<T> : DynamicOps<T>
{
    private readonly DynamicOps<T> _delegate;
    public RegistryAccess RegistryAccess { get; }

    public RegistryOps(DynamicOps<T> delegateOps, RegistryAccess registryAccess)
    {
        _delegate = delegateOps;
        RegistryAccess = registryAccess;
    }

    //GetRegistry looks up a registry by registry key, returning null if not found, maps to vanilla RegistryOps.owner/getter
    //Simplified to skip the HolderOwner/HolderGetter middle layer and return Registry<E> directly
    public Registry<E>? GetRegistry<E>(ResourceKey<Registry<E>> registryKey) where E : class
        => RegistryAccess.Lookup(registryKey);

    //DecodeHolder parses an Identifier then looks up a Holder from the registry, maps to vanilla retrieveElement
    //Simplified to parse with IdentifierCodec and then look up the registry to return a Reference Holder
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

    //EncodeId encodes a Holder as an Identifier string, the encoding path of vanilla HOLDER_ID_CODEC
    //Reference uses Key.Identifier while Direct throws, since Direct has no registry key
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

