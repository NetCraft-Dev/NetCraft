using System.Reflection;
using System.Runtime.Loader;

namespace NetCraft.ModLoader;

//ModAssemblies: mod assembly loading
//Handled uniformly: reuse if already in Default, since loading a second copy under the same name breaks type equality
internal static class ModAssemblies
{
    //Rewriter: the pre-load rewriter, set after bootstrap assembles the rules, loads as-is if not set
    //Mod-into-mod injection happens here; the injected mod must be swapped at the moment it loads
    internal static Func<string, byte[], byte[]>? Rewriter { get; set; }

    //_loading: assembly names currently loading, used to block infinite recursion from cyclic rules
    private static readonly HashSet<string> _loading = new(StringComparer.Ordinal);

    //Load: loads by assembly name and path, reusing the copy already in Default when present
    //Runs the rewriter before loading; mods matched by injection rules are swapped for the rewritten version here
    public static Assembly Load(string assemblyName, string assemblyPath)
    {
        var existing = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(a => a.GetName().Name == assemblyName);
        if (existing is not null)
            return existing;

        //When the replacement itself is injected it recurses back here; reentry with the same name means the rules have a cycle
        lock (_loading)
        {
            if (!_loading.Add(assemblyName))
                throw new InvalidOperationException($"assembly {assemblyName} is being loaded recursively, injection rules form a cycle");
        }

        try
        {
            var bytes = File.ReadAllBytes(assemblyPath);
            if (Rewriter is not null)
                bytes = Rewriter(assemblyName, bytes);
            return AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(bytes));
        }
        finally
        {
            lock (_loading)
            {
                _loading.Remove(assemblyName);
            }
        }
    }
}
