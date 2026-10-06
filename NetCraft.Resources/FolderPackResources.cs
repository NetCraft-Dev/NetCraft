using NetCraft.Registry;

namespace NetCraft.Resources;

//FolderPackResources, a folder-based resource pack, maps to vanilla net.minecraft.server.packs.FilePackResources
//Reads resources from a file system directory, assets/ maps to ClientResources and data/ maps to ServerData
public sealed class FolderPackResources : PackResources
{
    private readonly string _rootPath;
    //_files holds the relative paths of every file in the directory, all using '/', enumerated once at construction
    //Resource lookup is the hottest path during data loading and a File.Exists per call means hitting the disk every time
    //One full directory enumeration here replaces all later probes, turning the lookup into a pure in-memory set check
    private readonly HashSet<string> _files = new(StringComparer.Ordinal);

    public FolderPackResources(string packId, string rootPath) : base(packId)
    {
        _rootPath = rootPath;
        ScanFiles();
    }

    //ScanFiles enumerates all files under the root directory, normalizing relative paths to '/'
    private void ScanFiles()
    {
        if (!Directory.Exists(_rootPath)) return;
        foreach (var file in Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories))
            _files.Add(Path.GetRelativePath(_rootPath, file).Replace(Path.DirectorySeparatorChar, '/'));
    }

    //TypeToDir maps a resource type to its root directory name, aligned with the vanilla assets/data convention
    internal static string TypeToDir(PackType type) => type switch
    {
        PackType.ClientResources => "assets",
        PackType.ServerData => "data",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public override Stream? GetRootResource(string path)
    {
        var fullPath = Path.Combine(_rootPath, path);
        return File.Exists(fullPath) ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    public override Stream? GetResource(PackType type, Identifier location)
    {
        var relativePath = $"{TypeToDir(type)}/{location.Namespace}/{location.Path}";
        if (!_files.Contains(relativePath)) return null;
        var fullPath = Path.Combine(_rootPath, TypeToDir(type), location.Namespace, location.Path);
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public override void ListResources(PackType type, string namespaceName, string pathPrefix, ISet<Identifier> output)
    {
        var typeDir = Path.Combine(_rootPath, TypeToDir(type), namespaceName, pathPrefix);
        if (!Directory.Exists(typeDir)) return;
        foreach (var file in Directory.EnumerateFiles(typeDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(typeDir, file).Replace(Path.DirectorySeparatorChar, '/');
            output.Add(Identifier.FromNamespaceAndPath(namespaceName, pathPrefix + "/" + relative));
        }
    }

    public override ISet<string> GetNamespaces(PackType type)
    {
        var result = new HashSet<string>();
        var typeDir = Path.Combine(_rootPath, TypeToDir(type));
        if (!Directory.Exists(typeDir)) return result;
        foreach (var dir in Directory.EnumerateDirectories(typeDir))
        {
            result.Add(Path.GetFileName(dir));
        }
        return result;
    }
}
