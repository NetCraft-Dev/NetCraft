using System.Text.Json.Nodes;
using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Resources;

//RegistryData, data-driven registry declaration, maps to vanilla RegistryDataLoader.RegistryData
//Pairs "which registry" with "how the element is decoded" so RegistryDataLoader can scan and load by directory
//The non-generic base class lets declarations of different element types share one list
public abstract class RegistryData
{
    //RegistryId is the registry's own identifier, it determines the scan directory data/<namespace>/<RegistryId.Path>/
    public abstract Identifier RegistryId { get; }

    //TryLoadElement decodes one element JSON and writes it into the target registry, on failure the reason is written to error
    internal abstract bool TryLoadElement(Resource resource, Identifier elementId, RegistryAccess context, out string error);

    //Boxed, weakly typed registry loading, for registries declared as Registry<object> on the Registries side
    //The Registry project cannot reference the Game density function/noise settings types in reverse, those registries can only be declared with object keys
    //Elements decoded by the strongly typed codec are boxed and written into the object registry
    public static RegistryData Boxed<T>(WritableRegistry<object> registry, Codec<T> codec) where T : class
        => new BoxedRegistryData<T>(registry, codec);

    //Decode reads an element file and decodes it with the codec, a failure throws and the caller turns it into an error string
    //RegistryData<T> and BoxedRegistryData<T> differ only in how they write to the registry, reading and decoding share this one place
    //Goes straight through the JsonOps stream overload, which parses UTF-8 bytes and avoids the UTF-16 transcoding of ReadToEnd
    private protected static T Decode<T>(Resource resource, RegistryAccess context, Codec<T> codec) where T : class
    {
        JsonNode? node;
        using (var stream = resource.Open())
        {
            node = JsonOps.Parse(stream).GetOrThrow();
        }

        var ops = new RegistryOps<JsonNode?>(JsonOps.Instance, context);
        return codec.Parse(ops, node).GetOrThrow();
    }
}

//RegistryData<T>, registry data declaration for a concrete element type
public sealed class RegistryData<T> : RegistryData where T : class
{
    public RegistryData(WritableRegistry<T> registry, Codec<T> elementCodec)
    {
        Registry = registry;
        ElementCodec = elementCodec;
    }

    //Registry is the target registry, loading must happen before Freeze
    public WritableRegistry<T> Registry { get; }

    //ElementCodec, the element JSON codec
    public Codec<T> ElementCodec { get; }

    public override Identifier RegistryId => Registry.Key.Identifier;

    internal override bool TryLoadElement(Resource resource, Identifier elementId, RegistryAccess context, out string error)
    {
        error = string.Empty;
        try
        {
            var value = Decode(resource, context, ElementCodec);
            Registry.Register(ResourceKey<T>.Create(Registry.Key, elementId), value, RegistrationInfo.BuiltIn);
            //The element's own identifier can only be backfilled from the registry name, the vanilla id lives only in the registry
            if (value is RegistryIdentified identified) identified.SetRegistryId(elementId);
            return true;
        }
        catch (Exception ex)
        {
            error = $"{elementId}: {ex.Message}";
            return false;
        }
    }
}

//BoxedRegistryData<T>, loading implementation for strongly typed elements into a weakly typed registry
//No such form exists in vanilla, it is a compromise forced by the layering between this project's Registry and Game projects
internal sealed class BoxedRegistryData<T> : RegistryData where T : class
{
    private readonly WritableRegistry<object> _registry;
    private readonly Codec<T> _codec;

    public BoxedRegistryData(WritableRegistry<object> registry, Codec<T> codec)
    {
        _registry = registry;
        _codec = codec;
    }

    public override Identifier RegistryId => _registry.Key.Identifier;

    internal override bool TryLoadElement(Resource resource, Identifier elementId, RegistryAccess context, out string error)
    {
        error = string.Empty;
        try
        {
            var value = Decode(resource, context, _codec);
            _registry.Register(ResourceKey<object>.Create(_registry.Key, elementId), value, RegistrationInfo.BuiltIn);
            //The element's own identifier can only be backfilled from the registry name, the vanilla id lives only in the registry
            if (value is RegistryIdentified identified) identified.SetRegistryId(elementId);
            return true;
        }
        catch (Exception ex)
        {
            error = $"{elementId}: {ex.Message}";
            return false;
        }
    }
}
