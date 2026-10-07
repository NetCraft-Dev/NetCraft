using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen;

//ObjectMapCodecAdapter adapter from MapCodec<T> to MapCodec<object>
//Works around C# MapCodec<T> having no covariance so it cannot be cast to MapCodec<object> directly
//Wraps the inner MapCodec<T>, casts object to T to call the real DecodeEncodeTo and casts back to object
//Used to register the DensityFunction subclass MapCodec<DensityFunction> into Registry<MapCodec<object>>
public sealed class ObjectMapCodecAdapter<T> : AbstractMapCodec<object> where T : class
{
    private readonly MapCodec<T> _inner;

    public ObjectMapCodecAdapter(MapCodec<T> inner) { _inner = inner; }

    public override DataResult<object> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _inner.Decode(ops, input).Map(t => (object)t!);

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, object value, RecordBuilder<U> builder)
    {
        if (value is T typed)
            _inner.EncodeTo(ops, typed, builder);
        else
            builder.Add("error", DataResult<U>.Error(() => $"value is not of type {typeof(T).Name}").GetOrThrow());
        return builder;
    }
}

//DensityFunctionBootstrap density function bootstrap registration, maps to vanilla DensityFunctions.BOOTSTRAP
//Registers every subclass MapCodec in DensityFunctionCodecs.AllCodecs into the DENSITY_FUNCTION_TYPE registry
//The dispatch codec looks this registry up by type name; adding a type only needs one entry in AllCodecs
//Must be called before BuiltInRegistries.BootStrap or registration fails after Freeze
public static class DensityFunctionBootstrap
{
    private static bool _registered;

    //RegisterAll registers all DensityFunction subclass MapCodecs into DENSITY_FUNCTION_TYPE
    //Idempotent on repeated calls to avoid double registration
    public static void RegisterAll()
    {
        if (_registered) return;
        var registry = NetCraft.Registry.BuiltInRegistries.DENSITY_FUNCTION_TYPE;
        foreach (var (name, codec) in DensityFunctionCodecs.AllCodecs)
            Register(registry, name, codec);
        _registered = true;
    }

    //Register wraps MapCodec<DensityFunction> as MapCodec<object> and registers it into DENSITY_FUNCTION_TYPE
    private static void Register(NetCraft.Registry.Registry<NetCraft.Codec.MapCodec<object>> registry, string name, MapCodec<DensityFunction> codec)
    {
        var adapter = new ObjectMapCodecAdapter<DensityFunction>(codec);
        NetCraft.Registry.Registry<NetCraft.Codec.MapCodec<object>>.Register(registry, name, adapter);
    }

    public static void Reset()
    {
        _registered = false;
    }
}
