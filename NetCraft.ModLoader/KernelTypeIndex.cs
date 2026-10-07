using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace NetCraft.ModLoader;

//KernelTypeIndex: a type-to-assembly index used to locate an injection target in a specific assembly
//You cannot guess the assembly from the namespace prefix, the two do not correspond one to one
//For example, NetCraft.Game.Server.DedicatedServer lives in NetCraft.Server.dll
//The only reliable way is to read TypeDef records from the metadata tables without loading any assembly
//Not loading is essential for mod-into-mod injection; once a target is loaded early there is no chance to rewrite it
internal static class KernelTypeIndex
{
    //Build: scans the given assemblies to build the index; readBytes fetches raw bytes by assembly name and is skipped if unavailable
    public static Dictionary<string, string> Build(IEnumerable<string> assemblyNames, Func<string, byte[]?> readBytes)
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assemblyNames)
        {
            var bytes = readBytes(name);
            if (bytes is null)
                continue;

            Add(index, name, bytes);
        }
        return index;
    }

    //Add: merges a single assembly's types into the index
    //Same-named types are first come first served; the kernel is scanned first so it wins, and among mods the one scanned first wins
    public static void Add(Dictionary<string, string> index, string assemblyName, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata)
            return;

        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            index.TryAdd(GetFullName(reader, type), assemblyName);
        }
    }

    //GetFullName: builds the full type name; nested types are assembled outward as a.b/c
    private static string GetFullName(MetadataReader reader, TypeDefinition type)
    {
        var name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        if (!declaring.IsNil)
            return GetFullName(reader, reader.GetTypeDefinition(declaring)) + "/" + name;

        var ns = reader.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }
}
