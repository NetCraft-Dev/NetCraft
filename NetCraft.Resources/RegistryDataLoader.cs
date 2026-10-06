using NetCraft.Registry;

namespace NetCraft.Resources;

//RegistryDataLoader, data-driven registry loader, maps to vanilla RegistryDataLoader
//Scans resource packs at data/<namespace>/<registry path>/<element path>.json, decodes with the element codec and writes into the target registry
//A single bad element is only recorded in the result and does not abort the whole load, vanilla aggregates per-element exceptions into a CrashReport
public static class RegistryDataLoader
{
    //Load loads a set of registry data, must be called before the target registries are frozen
    //context provides the lookup entry point for resolving cross-registry references inside elements
    public static LoadResult Load(ResourceManager resourceManager, RegistryAccess context,
        IReadOnlyList<RegistryData> data)
    {
        //First scan the elements of all registries into one combined queue, elements reference each other, including across registries (configured_feature references placed_feature)
        //Vanilla loads registries in parallel so interleaved references resolve, here the whole queue is retried repeatedly to reproduce the same effect
        var pending = new List<PendingElement>();
        foreach (var entry in data) CollectPending(resourceManager, entry, pending);

        var loaded = 0;
        while (pending.Count > 0)
        {
            var progressed = 0;
            var remaining = new List<PendingElement>(pending.Count);
            foreach (var item in pending)
            {
                if (item.Data.TryLoadElement(item.Resource, item.Id, context, out var error))
                {
                    loaded++;
                    progressed++;
                }
                else
                {
                    //Keep this round's failure reason, drop it if the item succeeds in a later round, only record it as an error if it never succeeds
                    remaining.Add(item with { Error = error });
                }
            }
            //If a whole round makes no progress, the remaining items are real errors and will not change on retry
            if (progressed == 0) return new LoadResult(loaded, remaining.Select(item => item.Error).ToList());
            pending = remaining;
        }
        return new LoadResult(loaded, Array.Empty<string>());
    }

    //CollectPending scans all element files under a registry directory and queues them in directory order
    private static void CollectPending(ResourceManager resourceManager, RegistryData data,
        List<PendingElement> pending)
    {
        var folder = data.RegistryId.Path;
        var prefix = folder + "/";

        //Walk all namespaces, using the same pack traversal as tag loading
        foreach (var ns in resourceManager.GetNamespaces(PackType.ServerData))
        {
            foreach (var resource in resourceManager.ListResources(PackType.ServerData, ns, folder))
            {
                var path = resource.Location.Path;
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var elementPath = path[prefix.Length..];
                if (elementPath.EndsWith(".json", StringComparison.Ordinal))
                    elementPath = elementPath[..^5];
                if (elementPath.Length == 0) continue;

                var elementId = Identifier.FromNamespaceAndPath(resource.Location.Namespace, elementPath);
                pending.Add(new PendingElement(data, resource, elementId, string.Empty));
            }
        }
    }
}

//PendingElement, an element waiting to load, carries this round's failure reason for retry and final aggregation
internal readonly record struct PendingElement(RegistryData Data, Resource Resource, Identifier Id, string Error);

//LoadResult, carries the success count and per-element errors
public sealed class LoadResult
{
    public LoadResult(int loadedCount, IReadOnlyList<string> errors)
    {
        LoadedCount = loadedCount;
        Errors = errors;
    }

    //LoadedCount is the number of elements successfully written to a registry
    public int LoadedCount { get; }

    //Errors are per-element failure reasons, shaped like minecraft:foo: message
    public IReadOnlyList<string> Errors { get; }

    public bool HasErrors => Errors.Count > 0;
}
