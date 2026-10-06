using System.Reflection;
using System.Runtime.Loader;

namespace NetCraft;

//Embedded assembly loader (.NET-only technique).
//Through the AssemblyLoadContext.Resolving event, when a sub-library is not found by the runtime,
//load its byte stream from the main library's embedded resources (NetCraft.Embedded.*.dll).
//Maps to section 4 "Load sub-libraries via embedded resources" of the C# kernel rewrite plan.
public static class EmbeddedAssemblyLoader
{
    //Main library assembly (contains embedded resources).
    private static readonly Assembly MainAssembly = typeof(EmbeddedAssemblyLoader).Assembly;

    //Naming prefix for embedded resources, matching the csproj's LogicalName.
    private const string ResourcePrefix = "NetCraft.Embedded.";

    //KernelDirectoryName kernel assembly subdirectory name.
    //The kernel upper-layer assemblies Game/Server/Client/Gpu reference the main library in turn and cannot be embedded into it,
    //so by convention each executable project moves them after build into this subdirectory under the output directory, resolved on demand at runtime.
    public const string KernelDirectoryName = "kernel";

    //Whether already initialized.
    private static int _initialized;

    //Assembly byte rewriter: the mod loader uses it for injection rewrites; when unset, bytes load as-is.
    private static Func<string, byte[], byte[]>? _rewriter;

    //Set the byte rewriter. Takes the assembly name and original bytes, returns the rewritten bytes.
    //Must be set before the target assembly is first resolved; assemblies are resolved on demand, so being late misses the window.
    //Only affects embedded resources and the kernel directory handled by this loader; the main library itself goes through normal resolution and does not pass here, so ordinary mods cannot modify the main library.
    public static void SetRewriter(Func<string, byte[], byte[]>? rewriter)
    {
        _rewriter = rewriter;
    }

    //Register the embedded resource resolution callback. Should be called once at the earliest startup stage.
    //Idempotent: multiple calls take effect only once.
    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        var context = AssemblyLoadContext.Default;
        context.Resolving += OnResolvingAssembly;
    }

    //Callback on resolution failure: load bytes from embedded resources or the kernel directory.
    //Kernel assemblies were moved out of the output root; deps.json still registers them but the file is not found by path,
    //so after default resolution fails it lands here, the bytes pass through the rewriter for the first time, and mod injection happens at this moment.
    private static Assembly? OnResolvingAssembly(AssemblyLoadContext context, AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name))
        {
            return null;
        }

        var bytes = ReadAssemblyBytes(name.Name);
        if (bytes is null)
        {
            return null;
        }

        if (_rewriter is not null)
        {
            bytes = _rewriter(name.Name, bytes);
        }

        using var rewritten = new MemoryStream(bytes);
        return context.LoadFromStream(rewritten);
    }

    //ReadAssemblyBytes fetches original bytes by assembly name: embedded resources first, the kernel directory next, the run directory as fallback.
    //Returns null when not found. Both the preload-rewrite path and the resolution callback go through here, ensuring both paths get the same source.
    public static byte[]? ReadAssemblyBytes(string assemblyName)
    {
        var embedded = ReadEmbeddedAssembly(assemblyName);
        if (embedded is not null)
        {
            return embedded;
        }

        var kernelPath = Path.Combine(AppPaths.BaseDirectory, KernelDirectoryName, assemblyName + ".dll");
        if (File.Exists(kernelPath))
        {
            return File.ReadAllBytes(kernelPath);
        }

        //Run directory fallback: in output directories that skip the kernel distribution flow, such as tests, the dll still sits in the root
        var directPath = Path.Combine(AppPaths.BaseDirectory, assemblyName + ".dll");
        return File.Exists(directPath) ? File.ReadAllBytes(directPath) : null;
    }

    //Read only the bytes from embedded resources; returns null when absent.
    //To fetch kernel assembly bytes (embedded or kernel directory), use ReadAssemblyBytes.
    public static byte[]? ReadEmbeddedAssembly(string assemblyName)
    {
        using var stream = MainAssembly.GetManifestResourceStream(ResourcePrefix + assemblyName + ".dll");
        if (stream is null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    //List all loadable embedded sub-libraries (diagnostics/debug only).
    public static IReadOnlyList<string> ListEmbeddedAssemblies()
    {
        var result = new List<string>();
        foreach (var name in MainAssembly.GetManifestResourceNames())
        {
            if (name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".dll", StringComparison.Ordinal))
            {
                var assemblyName = name.Substring(ResourcePrefix.Length, name.Length - ResourcePrefix.Length - ".dll".Length);
                result.Add(assemblyName);
            }
        }
        return result;
    }
}
