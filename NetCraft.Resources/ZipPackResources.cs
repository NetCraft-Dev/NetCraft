using System.IO.Compression;
using NetCraft.Registry;

namespace NetCraft.Resources;

//ZipPackResources, a zip-compressed resource pack, maps to vanilla net.minecraft.server.packs.ZipPackResources
//Reads resources from a zip file following the assets/data directory convention
public sealed class ZipPackResources : PackResources
{
    private readonly ZipArchive _archive;
    private readonly Dictionary<PackType, HashSet<string>> _namespaces;

    public ZipPackResources(string packId, string zipPath) : base(packId)
    {
        _archive = ZipFile.Open(zipPath, ZipArchiveMode.Read);
        _namespaces = new()
        {
            [PackType.ClientResources] = new(),
            [PackType.ServerData] = new()
        };
        ScanNamespaces();
    }

    //ScanNamespaces scans assets/<ns>/ and data/<ns>/ inside the zip and collects the namespace set
    private void ScanNamespaces()
    {
        foreach (var entry in _archive.Entries)
        {
            var parts = entry.FullName.Split('/');
            if (parts.Length < 2) continue;
            if (parts[0] == "assets")
            {
                _namespaces[PackType.ClientResources].Add(parts[1]);
            }
            else if (parts[0] == "data")
            {
                _namespaces[PackType.ServerData].Add(parts[1]);
            }
        }
    }

    public override Stream? GetRootResource(string path)
    {
        var entry = _archive.GetEntry(path);
        return entry?.Open();
    }

    public override Stream? GetResource(PackType type, Identifier location)
    {
        var relativePath = $"{FolderPackResources.TypeToDir(type)}/{location.Namespace}/{location.Path}";
        var entry = _archive.GetEntry(relativePath);
        return entry?.Open();
    }

    public override void ListResources(PackType type, string namespaceName, string pathPrefix, ISet<Identifier> output)
    {
        var prefix = $"{FolderPackResources.TypeToDir(type)}/{namespaceName}/{pathPrefix}/";
        foreach (var entry in _archive.Entries)
        {
            if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var relative = entry.FullName[prefix.Length..];
            if (string.IsNullOrEmpty(relative)) continue;
            output.Add(Identifier.FromNamespaceAndPath(namespaceName, pathPrefix + "/" + relative));
        }
    }

    public override ISet<string> GetNamespaces(PackType type) => _namespaces[type];

    public override void Dispose()
    {
        _archive.Dispose();
    }
}
