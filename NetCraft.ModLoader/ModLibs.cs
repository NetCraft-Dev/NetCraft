using System.Reflection;
using System.Runtime.Loader;
using NetCraft.Logging;

namespace NetCraft.ModLoader;

//ModLibs: resolution of embedded mod dependencies, maps to vanilla Fabric's Jar-in-Jar
//Mods bundle dependency libraries as embedded resources in their own dll; when an assembly cannot be resolved it is taken from here
//Entries are all .dll resources in the assembly; at build time libraries added with dotnet add package are embedded directly
//Kernel sub-library resolution is handled by EmbeddedAssemblyLoader; this only handles the mod's own part
//Registration must happen before ModHooks.Build; assembly needs to resolve replacement classes, so dependencies must be available at that moment
public static class ModLibs
{
    //_entries: embedded dependencies of each mod, one resource name per entry
    private static readonly List<(string ModPath, string ResourceName)> _entries = new();
    private static int _registered;

    //Register: collects embedded dependencies and hooks up the resolution callback
    //Entries come from all .dll resources embedded in the assembly; the template project's build embeds dependencies directly
    //Only mods with a matching environment are collected; mods with a mismatched side are not loaded later either
    //Collect entries before deciding whether to hook the callback; reversing the order would leave it never hooked if the first call has no entries
    public static void Register(IEnumerable<ScannedMod> mods)
    {
        foreach (var mod in mods)
        {
            foreach (var resource in mod.EmbeddedResources)
            {
                if (resource.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    _entries.Add((mod.AssemblyPath, resource));
            }
        }

        if (_entries.Count == 0)
            return;

        //The callback is hooked only once, entries can keep being added
        if (Interlocked.Exchange(ref _registered, 1) == 1)
            return;

        AssemblyLoadContext.Default.Resolving += OnResolving;
        Log.Info($"Embedded mod dependencies registered: {_entries.Count}");
    }

    //OnResolving: takes from the mod's embedded resources when the kernel side fails to resolve
    private static Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name)
    {
        var bytes = TryRead(name.Name);
        if (bytes is null)
            return null;

        Log.Debug($"Resolved assembly {name.Name} from embedded mod resources");
        return context.LoadFromStream(new MemoryStream(bytes));
    }

    //TryRead: looks up by assembly name among the registered entries
    public static byte[]? TryRead(string? assemblyName) => TryReadFrom(_entries, assemblyName);

    //TryReadFrom: looks up by assembly name in a set of entries, returns null if not found
    //Extracted so a set of entries can be fed in directly for verification without going through registration
    public static byte[]? TryReadFrom(IEnumerable<(string ModPath, string ResourceName)> entries, string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return null;

        foreach (var (modPath, resourceName) in entries)
        {
            if (!MatchesLibName(assemblyName, resourceName))
                continue;

            var bytes = ModScanner.ReadEmbeddedResource(modPath, resourceName);
            if (bytes is not null)
                return bytes;
        }
        return null;
    }

    //MatchesLibName: whether a resource name and an assembly name refer to the same library
    //The resource name is usually MyLib.dll
    //Embedded resources with the default name carry a project namespace prefix, so a suffix match must be accepted too
    public static bool MatchesLibName(string assemblyName, string resourceName)
    {
        var trimmed = resourceName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? resourceName[..^4]
            : resourceName;

        return trimmed.Equals(assemblyName, StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("." + assemblyName, StringComparison.OrdinalIgnoreCase);
    }
}
