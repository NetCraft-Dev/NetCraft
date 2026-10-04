using System.Text.Json;
using NetCraft.Logging;

namespace NetCraft.Game;

//LanguageAssets 语言素材准备 把资源对象里的语言表与语言清单落到程序根目录
//原版客户端 jar 只带 en_us 其余语言放在资源服务器的对象存储 由启动器按资源索引下载
//这里按同一份索引取 本地对象已有且大小匹配就直接复制 缺的从资源服务器补回
//只处理语言相关条目 贴图模型等素材仍由 jar 解压提供
public static class LanguageAssets
{
    //ObjectHost 资源对象地址前缀 与启动器使用的完全一致
    private const string ObjectHost = "https://resources.download.minecraft.net";

    //Client 对象下载客户端 复用连接 只在本地缺对象时才用到
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    //Prepare 按资源索引把语言素材落到程序根目录 返回落下的文件数
    //assetsDir 是启动器那级的 assets 目录 内含 indexes 与 objects
    //assetIndex 为空时取 indexes 目录里最近修改的一份
    //targetRoot 为空时落程序根目录 测试可指定临时目录避免污染
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

    //LocateIndex 定位资源索引文件 未指定名字时取 indexes 目录里最近修改的一份
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

    //IsWanted 只挑语言表与语言清单 其余素材由 jar 解压提供
    private static bool IsWanted(string sourcePath)
        => sourcePath.Equals("pack.mcmeta", StringComparison.Ordinal)
           || (sourcePath.StartsWith("minecraft/lang/", StringComparison.Ordinal)
               && sourcePath.EndsWith(".json", StringComparison.Ordinal));

    //MapTarget 索引里的源路径映射到目标根的落地位置
    //pack.mcmeta 这类根文件直接落根 其余按 assets/<路径> 还原成资源包结构
    private static string MapTarget(string sourcePath, string root)
    {
        var relative = sourcePath.Contains('/')
            ? Path.Combine("assets", sourcePath.Replace('/', Path.DirectorySeparatorChar))
            : sourcePath;
        return Path.Combine(root, relative);
    }

    //EnsureObject 保证对象在本地且大小匹配 缺的从资源服务器取回 失败返回 null
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
