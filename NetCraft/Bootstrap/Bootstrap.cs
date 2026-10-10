using System.Reflection;
using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Registry;

namespace NetCraft.Bootstrap;

//Bootstrap static class, maps to vanilla net.minecraft.server.Bootstrap
//Triggers the bootstrap callback of all built-in registries before kernel startup
//Validates default values and completes early content population
public static class Bootstrap
{
    private static bool _bootstrapped;

    static Bootstrap() => Log.SetClassSource(typeof(Bootstrap));

    //BootStrap main entry; triggers all built-in registry bootstrap callbacks and validates
    public static void BootStrap()
    {
        if (_bootstrapped)
        {
            //Log.Debug("BootStrap exit");
            return;
        }
        //Log.Debug("BootStrap entry");
        BuiltInRegistries.BootStrap();
        ValidateRegistries();
        _bootstrapped = true;
        Log.Info("Bootstrap complete, registries validated");
        //Log.Debug("BootStrap exit");
    }

    public static bool IsBootstrapped => _bootstrapped;

    public static void Reset()
    {
        _bootstrapped = false;
    }

    //ValidateRegistries reflects over all Registry fields of BuiltInRegistries
    //Calls Freeze on each to make them immutable, counts the total registrations, and Log.Warning reports empty registries
    public static void ValidateRegistries()
    {
        //Log.Debug($"ValidateRegistries entry");
        int total = 0;
        int emptyCount = 0;
        int regCount = 0;
        foreach (var (_, registry) in BuiltInRegistries.EnumerateRegistries())
        {
            //Use reflection for Freeze and Size to avoid generic covariance restrictions
            registry.GetType().GetMethod("Freeze")?.Invoke(registry, null);
            var sizeProp = registry.GetType().GetProperty("Size");
            int count = (int)(sizeProp?.GetValue(registry) ?? 0);
            regCount++;
            total += count;
            if (count == 0) emptyCount++;
        }
        Log.Info($"Registry validation complete: {regCount} registries, {total} entries, {emptyCount} empty");
        //Log.Debug($"ValidateRegistries exit");
    }

    //LoadBuiltinTags loads the built-in tag collection from the resource manager
    //Creates a TagLoader for each core registry and scans the tags/{category} directory
    //After BuildAll registers into TagManager, awaiting binding by BindAll
    //category is the path segment of the registry key; vanilla data packs split directories by this
    //(block/item/entity_type/fluid/game_event are singular; biomes are under worldgen/biome)
    public static void LoadBuiltinTags(TagManager tagManager, ResourceManager resourceManager)
    {
        Log.Debug($"LoadBuiltinTags entry tagManager={tagManager} resourceManager={resourceManager}");
        int totalTags = 0;
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.BLOCK, "block");
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.ITEM, "item");
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.ENTITY_TYPE, "entity_type");
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.FLUID, "fluid");
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.GAME_EVENT, "game_event");
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.BIOME, "worldgen/biome");
        Log.Info($"Scanned built-in tags: {totalTags} tag files registered to TagManager");
        //Log.Debug("LoadBuiltinTags exit");
    }

    //LoadTagsForRegistry loads tag files for a single registry and registers them into TagManager
    //The generic T aligns with the registry element type; the elementGetter delegate points to registry.Get(id)
    private static int LoadTagsForRegistry<T>(
        TagManager tagManager,
        ResourceManager resourceManager,
        Registry<T> registry,
        string category) where T : class
    {
        var loader = new TagLoader<T>($"tags/{category}", id =>
        {
            var holder = registry.Get(id);
            return holder is not null ? Optional<T>.Of(holder.Value) : Optional<T>.Empty();
        });

        var files = LoadTagFiles(resourceManager, category);
        if (files.Count == 0) return 0;

        var builtTags = loader.BuildAll(files);
        tagManager.RegisterLoader(registry.Key, loader, builtTags);
        return files.Count;
    }

    //LoadTagFiles scans TagFiles in the tags/{category} directory under all namespaces
    //Returns a tag id -> TagFile list map; multiple data packs for the same tag are merged
    private static Dictionary<Identifier, List<TagFile>> LoadTagFiles(ResourceManager resourceManager, string category)
    {
        var result = new Dictionary<Identifier, List<TagFile>>();
        var prefix = $"tags/{category}/";
        foreach (var ns in resourceManager.GetNamespaces(PackType.ServerData))
        {
            foreach (var resource in resourceManager.ListResources(PackType.ServerData, ns, $"tags/{category}"))
            {
                var path = resource.Location.Path;
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var tagPath = path[prefix.Length..];
                if (tagPath.EndsWith(".json", StringComparison.Ordinal))
                    tagPath = tagPath[..^5];
                if (tagPath.Length == 0) continue;
                var tagId = Identifier.FromNamespaceAndPath(resource.Location.Namespace, tagPath);

                using var stream = resource.Open();
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                TagFile tagFile;
                try
                {
                    tagFile = TagFile.FromJson(json);
                }
                catch (Exception ex)
                {
                    Log.Warning($"Skipping unparsable tag file {tagId}: {ex.Message}");
                    continue;
                }

                if (!result.TryGetValue(tagId, out var list))
                {
                    list = new List<TagFile>();
                    result[tagId] = list;
                }
                list.Add(tagFile);
            }
        }
        return result;
    }
}
