using NetCraft.Logging;

namespace NetCraft.Registry;

//Registry implementation with a default value; missing key/id falls back to the value for defaultKey
public class DefaultedMappedRegistry<T> : MappedRegistry<T>, DefaultedRegistry<T> where T : class
{
    private readonly Identifier _defaultKey;
    private Reference<T>? _defaultValue;

    public DefaultedMappedRegistry(string defaultKey, ResourceKey<Registry<T>> key, Lifecycle lifecycle)
        : base(key, lifecycle)
    {
        _defaultKey = Identifier.Parse(defaultKey);
    }

    //Cache the default Holder when the key equals defaultKey at registration
    public override Reference<T> Register(ResourceKey<T> key, T value, RegistrationInfo registrationInfo)
    {
        //Log.Debug($"Register enter key={key} value={value} registrationInfo={registrationInfo}");
        var result = base.Register(key, value, registrationInfo);
        if (_defaultKey.Equals(key.Identifier))
            _defaultValue = result;
       // Log.Debug($"Register exit result={result}");
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

    //HolderLookup.Get looks up a Holder by ResourceKey; unregistered falls back to the default Holder, aligning with vanilla DefaultedRegistry.get
    public override Holder<T>? Get(ResourceKey<T> key)
        => base.Get(key) ?? _defaultValue;

    //Vanilla overrides getOptional to skip the default fallback
    public T? GetOptional(Identifier key) => base.GetValue(key);

    public override Reference<T>? GetAny() => _defaultValue;

    public override T? ById(int id)
        => base.ById(id) ?? _defaultValue?.Value;

    //TODO RandomSource: getRandom falls back to default when not found

    public Identifier DefaultKey => _defaultKey;
}
