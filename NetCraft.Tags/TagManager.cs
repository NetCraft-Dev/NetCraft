using NetCraft.Logging;
using NetCraft.Registry;

namespace NetCraft.Tags;

//ITagLoader non-generic marker interface
//Works around C# generic invariance, TagLoader<Block> cannot be cast to TagLoader<object>
//Actual access goes through this interface to avoid type incompatibility
public interface ITagLoader { }

//TagManager global tag manager, maps to vanilla net.minecraft.tags.TagManager
//Holds the TagLoader and built results of each registry, BindAll walks them and calls Registry.BindTags
//_loaders uses Identifier as the key to avoid C# generic invariance, ResourceKey<Registry<T>> cannot be cast to ResourceKey<Registry<object>>
public sealed class TagManager
{
    //loaders TagLoader instances of each registry, indexed by the registry Identifier
    private readonly Dictionary<Identifier, ITagLoader> _loaders = new();
    //binders binding closures of each registry, capture the T type to perform the BuildAll->BindTags conversion
    private readonly List<Action<RegistryAccess>> _binders = new();

    //RegisterLoader registers a registry's tag loader and its built results
    //builtTags is the result the caller obtains first via loader.LoadDirectory + BuildAll
    public void RegisterLoader<T>(
        ResourceKey<Registry<T>> registryKey,
        TagLoader<T> loader,
        IReadOnlyDictionary<Identifier, List<T>> builtTags)
        where T : class
    {
        Log.Debug($"RegisterLoader entry registryKey={registryKey} loader={loader} builtTags={builtTags}");
        _loaders[registryKey.Identifier] = loader;
        _binders.Add(access => BindLoaderForRegistry(access, registryKey, builtTags));
        //Log.Debug($"RegisterLoader exit");
    }

    //GetLoader gets the tag loader of a registry
    public TagLoader<T>? GetLoader<T>(ResourceKey<Registry<T>> registryKey)
        where T : class
    {
        Log.Debug($"GetLoader entry registryKey={registryKey}");
        TagLoader<T>? result = null;
        if (_loaders.TryGetValue(registryKey.Identifier, out var loader) && loader is TagLoader<T> typed)
        {
            result = typed;
        }
        Log.Debug($"GetLoader exit result={result}");
        return result;
    }

    //BindLoaderForRegistry converts builtTags into a TagKey->Holder list and calls Registry.BindTags
    //The closure captures the T type to avoid losing the generic argument after type erasure
    private static void BindLoaderForRegistry<T>(
        RegistryAccess access,
        ResourceKey<Registry<T>> registryKey,
        IReadOnlyDictionary<Identifier, List<T>> builtTags)
        where T : class
    {
        var registry = access.Lookup<T>(registryKey);
        if (registry is null) return;

        var pendingTags = new Dictionary<TagKey<T>, IReadOnlyList<Holder<T>>>();
        foreach (var (tagId, values) in builtTags)
        {
            var tagKey = TagKey<T>.Create(registryKey, tagId);
            var holders = values.Select(v => registry.WrapAsHolder(v)).ToList();
            pendingTags[tagKey] = holders;
        }
        registry.BindTags(pendingTags);
    }

    //BindAll binds the tags of every registry to the corresponding Registry
    public void BindAll(RegistryAccess access)
    {
        Log.Debug($"BindAll entry access={access}");
        foreach (var binder in _binders)
            binder(access);
        //Log.Debug($"BindAll exit");
    }

    //Reset clears all loaders and built tags
    public void Reset()
    {
        //Log.Debug($"Reset entry");
        _loaders.Clear();
        _binders.Clear();
        //Log.Debug($"Reset exit");
    }
}
