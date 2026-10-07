using System.Collections.Concurrent;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureTemplateManager structure template manager, maps to the identically named vanilla class
//Reads templates by id from the datapack path data/<ns>/structure/<path>.nbt; every result (including failures) is cached
public sealed class StructureTemplateManager
{
    //Directory template directory, maps to vanilla FileToIdConverter("structure", ".nbt")
    public const string Directory = "structure";

    public const string Extension = ".nbt";

    private readonly ResourceManager _resources;
    //Template cache; generation advances several chunks in parallel, and concurrent writes to a plain dictionary can cycle the bucket chain, manifesting as an insert hanging for tens of seconds
    //Wrapping in Lazy: a template is parsed only once even when requested concurrently, and failures are cached too
    private readonly ConcurrentDictionary<Identifier, Lazy<StructureTemplate?>> _cache = new();

    public StructureTemplateManager(ResourceManager resources) => _resources = resources;

    //GetOrLoad returns the template for an id, or null when absent; the failure is cached to avoid rescanning resource packs
    public StructureTemplate? GetOrLoad(Identifier id)
        => _cache.GetOrAdd(id,
            static (key, self) => new Lazy<StructureTemplate?>(
                () => self.Load(key), LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;

    //Load builds the template path and reads the NBT
    private StructureTemplate? Load(Identifier id)
    {
        var location = Identifier.FromNamespaceAndPath(id.Namespace, $"{Directory}/{id.Path}{Extension}");
        var resource = _resources.GetResource(PackType.ServerData, location);
        if (resource is null) return null;
        try
        {
            using var stream = resource.Open();
            var tag = NbtIo.ReadCompressed(stream, NbtAccounter.UnlimitedHeap());
            var template = new StructureTemplate();
            template.Load(tag);
            return template;
        }
        catch (Exception e)
        {
            Log.Warning($"Structure template {id} failed to read: {e.Message}");
            return null;
        }
    }
}
