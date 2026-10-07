using System.Text.Json;
using NetCraft.Logging;

namespace NetCraft.Game;

//LanguageAssets language asset preparation; lands the language table and language manifest from resource objects into the program root
//The vanilla client jar only ships en_us; the other languages live in the resource server's object storage and are downloaded by the launcher by resource index
//Here the same index is used: if the local object exists and the size matches it is copied directly, otherwise it is fetched from the resource server
//Only language-related entries are handled; textures, models and the like are still provided by jar extraction
public static class LanguageAssets
{
    //ObjectHost resource object URL prefix, exactly the one the launcher uses
    private const string ObjectHost = "https://resources.download.minecraft.net";

    //Client object download client, reusing the connection; only used when the local object is missing
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    //Prepare lands the language assets into the program root by resource index and returns the number of files landed
    //assetsDir is the launcher-level assets directory, containing indexes and objects
    //When assetIndex is empty the most recently modified one in the indexes directory is used
    //When targetRoot is empty it lands in the program root; tests can pass a temp directory to avoid pollution
    public static int Prepare(string assetsDir, string assetIndex = "", string? targetRoot = null)
    {
        if (string.IsNullOrEmpty(assetsDir) || !Directory.Exists(assetsDir)) return 0;
        var indexFile = LocateIndex(assetsDir, assetIndex);
        if (indexFile is null)
        {
            Log.Warning($"Asset index not found, language assets skipped {assetsDir}");
            return 0;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(indexFile));
        if (!document.RootElement.TryGetProperty("objects", out var objects)) return 0;

        var root = string.IsNullOrEmpty(targetRoot) ? AppPaths.BaseDirectory : targetRoot;
        var objectsDir = Path.Combine(assetsDir, "objects");
        var dropped = 0;
        var copied = 0;
        foreach (var entry in objects.EnumerateObject())
        {
            if (!IsWanted(entry.Name)) continue;
            if (!entry.Value.TryGetProperty("hash", out var hashNode)) continue;
            var hash = hashNode.GetString();
            if (string.IsNullOrEmpty(hash)) continue;
            var size = entry.Value.TryGetProperty("size", out var sizeNode) ? sizeNode.GetInt64() : -1L;

            var target = MapTarget(entry.Name, root);
            if (File.Exists(target) && (size < 0 || new FileInfo(target).Length == size)) continue;

            var source = EnsureObject(objectsDir, hash, size);
            if (source is null) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            dropped++;
            if (source.StartsWith(objectsDir, StringComparison.Ordinal)) copied++;
        }

        Log.Info($"Language assets ready index {Path.GetFileName(indexFile)} dropped {dropped} of which fetched {copied}");
        return dropped;
    }

    //LocateIndex locates the resource index file; without a name it uses the most recently modified one in the indexes directory
    private static string? LocateIndex(string assetsDir, string assetIndex)
    {
        var indexes = Path.Combine(assetsDir, "indexes");
        if (!Directory.Exists(indexes)) return null;
        if (!string.IsNullOrEmpty(assetIndex))
        {
            var named = Path.Combine(indexes, assetIndex + ".json");
            return File.Exists(named) ? named : null;
        }
        return Directory.EnumerateFiles(indexes, "*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    //IsWanted only picks the language tables and language manifest; other assets are provided by jar extraction
    private static bool IsWanted(string sourcePath)
        => sourcePath.Equals("pack.mcmeta", StringComparison.Ordinal)
           || (sourcePath.StartsWith("minecraft/lang/", StringComparison.Ordinal)
               && sourcePath.EndsWith(".json", StringComparison.Ordinal));

    //MapTarget maps the source path in the index to its landing location under the target root
    //Root files such as pack.mcmeta land at the root; the rest restore the resource pack structure as assets/<path>
    private static string MapTarget(string sourcePath, string root)
    {
        var relative = sourcePath.Contains('/')
            ? Path.Combine("assets", sourcePath.Replace('/', Path.DirectorySeparatorChar))
            : sourcePath;
        return Path.Combine(root, relative);
    }

    //EnsureObject ensures the object is local with a matching size, fetching from the resource server when missing; returns null on failure
    private static string? EnsureObject(string objectsDir, string hash, long size)
    {
        var path = Path.Combine(objectsDir, hash[..2], hash);
        if (File.Exists(path) && (size < 0 || new FileInfo(path).Length == size)) return path;

        var url = $"{ObjectHost}/{hash[..2]}/{hash}";
        try
        {
            var bytes = Client.GetByteArrayAsync(url).GetAwaiter().GetResult();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return path;
        }
        catch (Exception ex)
        {
            Log.Warning($"Language asset fetch failed {hash} {ex.Message}");
            return null;
        }
    }
}
