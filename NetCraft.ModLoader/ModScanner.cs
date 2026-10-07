using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

namespace NetCraft.ModLoader;

//ScannedMod: a mod entry produced by static scanning
public sealed class ScannedMod
{
    //AssemblyPath: the mod dll path
    public string AssemblyPath { get; init; } = string.Empty;
    //AssemblyName: the assembly name, used to map AssemblyRef back to the mod
    public string AssemblyName { get; init; } = string.Empty;
    //Manifest: the parse result of the embedded ncmod.json
    public ModManifest Manifest { get; init; } = new();
    //EmbeddedResources: all embedded resource names in this assembly
    //A mod's third-party dependencies are embedded as resources directly by the build; dependency resolution relies on this list
    //Listed so resolution does not need to open the file again
    public IReadOnlyList<string> EmbeddedResources { get; init; } = Array.Empty<string>();
    //ReferencedAssemblies: assembly names this assembly references
    public IReadOnlyList<string> ReferencedAssemblies { get; init; } = Array.Empty<string>();
    //AnnotatedHooks: injection rules statically scanned from Inject annotations
    //Same shape as the manifest hooks; both paths merge at assembly time, and when both declare the same injection point the annotation wins
    public IReadOnlyList<ModHookRule> AnnotatedHooks { get; init; } = Array.Empty<ModHookRule>();
    //AnnotatedMixins: mixin rules statically scanned from Mixin annotations, same shape as the manifest mixins
    public IReadOnlyList<ModMixinRule> AnnotatedMixins { get; init; } = Array.Empty<ModMixinRule>();
}

//ModScanner: the static mod scanner
//Uses MetadataReader to read the dll's metadata tables and embedded resources without loading any assembly
//Loading an assembly by itself does not trigger reference resolution, but resolving a type inside the mod pulls up the kernel along the way
//Statically scanning the environment side first before deciding what to load is a prerequisite under a single ALC, since a loaded assembly cannot be unloaded
public static class ModScanner
{
    //ManifestResourceName: the fixed embedded resource name used for the mod declaration
    public const string ManifestResourceName = "ncmod.json";

    //InjectHostAssembly: the assembly containing the Inject annotation
    //A mod that does not reference it cannot have annotations, so iteration is skipped, saving the cost of scanning the attribute table of every method
    private const string InjectHostAssembly = "NetCraft.ModApi";

    //ScanAll: scans all dlls in the directory, skipping those without a declaration
    public static List<ScannedMod> ScanAll(string folderPath)
    {
        var result = new List<ScannedMod>();
        if (!Directory.Exists(folderPath))
            return result;

        foreach (var path in Directory.GetFiles(folderPath, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var scanned = Scan(path);
            if (scanned is not null)
                result.Add(scanned);
        }
        return result;
    }

    //Scan: scans one dll, returns null when there is no embedded declaration or it is not a managed assembly
    public static ScannedMod? Scan(string dllPath)
    {
        try
        {
            using var stream = File.OpenRead(dllPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
                return null;

            var reader = pe.GetMetadataReader();
            var manifestBytes = ReadResource(pe, reader, ManifestResourceName);
            if (manifestBytes is null)
                return null;

            var manifest = JsonSerializer.Deserialize<ModManifest>(manifestBytes);
            if (manifest is null || manifest.Id.Length == 0)
                return null;

            var assemblyName = reader.GetString(reader.GetAssemblyDefinition().Name);
            var referenced = reader.AssemblyReferences
                .Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))
                .ToList();

            //Annotation rules and manifest rules are scanned in the same pass and merged into one table at assembly time with no particular order
            var annotated = new List<ModHookRule>();
            var annotatedMixins = new List<ModMixinRule>();
            if (referenced.Contains(InjectHostAssembly, StringComparer.Ordinal))
            {
                annotated = InjectScanner.Scan(reader);
                annotatedMixins = InjectScanner.ScanMixins(reader);
            }

            return new ScannedMod
            {
                AssemblyPath = dllPath,
                AssemblyName = assemblyName,
                Manifest = manifest,
                EmbeddedResources = ListResourceNames(reader),
                ReferencedAssemblies = referenced,
                AnnotatedHooks = annotated,
                AnnotatedMixins = annotatedMixins,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    //ReadEmbeddedResource: reads any embedded resource bytes, used by dependency resolution and diagnostics
    //Does not swallow exceptions; the caller handles a non-managed assembly or a missing resource
    public static byte[]? ReadEmbeddedResource(string dllPath, string resourceName)
    {
        using var stream = File.OpenRead(dllPath);
        using var pe = new PEReader(stream);
        return pe.HasMetadata ? ReadResource(pe, pe.GetMetadataReader(), resourceName) : null;
    }

    //ListResourceNames: lists embedded resource names, resource entries pointing at external files are excluded
    private static List<string> ListResourceNames(MetadataReader reader)
    {
        var names = new List<string>();
        foreach (var handle in reader.ManifestResources)
        {
            var resource = reader.GetManifestResource(handle);
            if (resource.Implementation.IsNil)
                names.Add(reader.GetString(resource.Name));
        }
        return names;
    }

    //ReadResource: reads embedded resource bytes, returns null when the resource is missing or points at an external file
    //ManifestResource.Offset is an offset relative to the CLI resources directory RVA, not a file offset
    //The data in the directory begins with a 4-byte length, so it must be read from the block returned by GetSectionData(resources directory RVA) at that offset
    private static byte[]? ReadResource(PEReader pe, MetadataReader reader, string resourceName)
    {
        var resourcesRva = pe.PEHeaders.CorHeader?.ResourcesDirectory.RelativeVirtualAddress ?? 0;
        if (resourcesRva == 0)
            return null;

        var block = pe.GetSectionData(resourcesRva);
        foreach (var handle in reader.ManifestResources)
        {
            var resource = reader.GetManifestResource(handle);
            if (!resource.Implementation.IsNil)
                continue;
            if (reader.GetString(resource.Name) != resourceName)
                continue;

            var offset = (int)resource.Offset;
            var length = block.GetReader(offset, sizeof(int)).ReadInt32();
            return block.GetContent(offset + sizeof(int), length).ToArray();
        }
        return null;
    }
}
