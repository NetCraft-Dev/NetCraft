using System.IO.Compression;
using System.Linq;
using NetCraft.Logging;

namespace NetCraft.Game;

//AssetsExtractor 素材辅助函数
//从启动参数指定的 jar 路径和已下载音频目录提取资源到程序根目录的 assets
//jar 仅提取 assets/ 子目录前缀条目去掉前缀释放到根/assets 保持原版资源结构
//核心业务不依赖本类直接从 ExtractedJarDir/ExtractedSoundsDir 加载实现业务解耦
//启动参数 --jar-path 指定 jar 文件路径 --sounds-dir 指定音频目录
public static class AssetsExtractor
{
    //ExtractedJarDir 业务加载 jar 资源的固定目录根/assets
    public const string ExtractedJarDir = "assets";

    //ExtractedSoundsDir 业务加载音频资源的固定目录根/assets/sounds
    public const string ExtractedSoundsDir = "assets/sounds";

    //Extract 从 GameOptions 读取路径参数提取 jar 和音频到固定目录
    //返回 true 表示至少一项提取成功 false 表示所有项都未配置或失败
    public static bool Extract(GameOptions options)
    {
        Log.SetClassSource(typeof(AssetsExtractor));

        bool any = false;

        var jarPath = options.GetOptionOrDefault("jar-path", string.Empty);
        if (!string.IsNullOrEmpty(jarPath))
        {
            //检测 assets 目录已存在且非空则跳过提取避免每次启动重复解压
            if (IsDirNonEmpty(AppPaths.AssetsDir))
            {
                Log.Info($"assets directory already exists, skipping jar extraction {AppPaths.AssetsDir}");
                any = true;
            }
            else if (TryExtractJar(jarPath, AppPaths.AssetsDir, AppPaths.DataDir, AppPaths.BaseDirectory))
            {
                any = true;
            }
        }
        else
        {
            Log.Info("--jar-path not specified, skipping jar extraction");
        }

        var soundsDir = options.GetOptionOrDefault("sounds-dir", string.Empty);
        if (!string.IsNullOrEmpty(soundsDir))
        {
            var target = Path.Combine(AppPaths.AssetsDir, "sounds");
            //检测 sounds 目录已存在且非空则跳过复制避免每次启动重复复制
            if (IsDirNonEmpty(target))
            {
                Log.Info($"sounds directory already exists, skipping audio copy {target}");
                any = true;
            }
            else if (TryCopySounds(soundsDir, target))
            {
                any = true;
            }
        }
        else
        {
            Log.Info("--sounds-dir not specified, skipping audio copy");
        }

        if (any)
        {
            Log.Info($"Asset extraction finished assets={AppPaths.AssetsDir} data={AppPaths.DataDir}");
        }
        return any;
    }

    //TryExtractJar 旧签名委托新签名传 dataDir=null rootDir=null 保持向后兼容
    //仅提取 assets/ 前缀不提取 data/ pack.mcmeta 复制到 assetsDir 旧位置
    public static bool TryExtractJar(string jarPath, string targetDir)
        => TryExtractJar(jarPath, targetDir, null, null);

    //TryExtractJar 解压 jar 内 assets/ 与 data/ 前缀条目到各自目标目录
    //assets/ 去前缀到 assetsDir 保留 assets/<ns>/... 子结构
    //data/ 去前缀到 dataDir 保留 data/<ns>/... 子结构 dataDir 为 null 时跳过
    //根 pack.mcmeta 复制到 rootDir 与 assets/data 同级供 FolderPackResources 读取
    //rootDir 为 null 时回退到 assetsDir 兼容旧行为
    //跳过 class/META-INF 等无关条目 jar 不存在或路径无效返回 false 不抛异常
    public static bool TryExtractJar(string jarPath, string assetsDir, string? dataDir, string? rootDir)
    {
        if (!File.Exists(jarPath))
        {
            Log.Warning($"jar file not found {jarPath}");
            return false;
        }
        try
        {
            Directory.CreateDirectory(assetsDir);
            if (dataDir is not null) Directory.CreateDirectory(dataDir);
            using var archive = ZipFile.OpenRead(jarPath);
            int assetsCount = 0;
            int dataCount = 0;
            bool packMcmetaCopied = false;
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var fullName = entry.FullName;
                //部分 jar（反编译打包）条目带 resources/ 前缀先去掉再走 pack.mcmeta/assets/data 匹配
                var entryPath = fullName.StartsWith("resources/", StringComparison.OrdinalIgnoreCase)
                    ? fullName.Substring("resources/".Length)
                    : fullName;
                //根 pack.mcmeta 复制到 rootDir 与 assets/data 同级供 FolderPackResources 读取
                //rootDir 为 null 时回退到 assetsDir 兼容旧行为
                if (entryPath.Equals("pack.mcmeta", StringComparison.OrdinalIgnoreCase))
                {
                    var dest = Path.Combine(rootDir ?? assetsDir, "pack.mcmeta");
                    entry.ExtractToFile(dest, overwrite: true);
                    packMcmetaCopied = true;
                    continue;
                }
                //assets/ 前缀去前缀到 assetsDir
                if (entryPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = entryPath.Substring("assets/".Length);
                    ExtractEntry(entry, assetsDir, rel);
                    assetsCount++;
                    continue;
                }
                //data/ 前缀去前缀到 dataDir dataDir 为 null 时跳过
                if (dataDir is not null && entryPath.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = entryPath.Substring("data/".Length);
                    ExtractEntry(entry, dataDir, rel);
                    dataCount++;
                    continue;
                }
                //跳过 class/META-INF 等无关条目
            }
            Log.Info($"jar extraction finished assets={assetsCount} data={dataCount} pack.mcmeta={packMcmetaCopied} {jarPath} -> assets={assetsDir} data={dataDir ?? "(skipped)"}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning($"jar extraction failed {jarPath} {ex.Message}");
            return false;
        }
    }

    //ExtractEntry 把 zip 条目释放到目标目录保留相对子结构
    private static void ExtractEntry(System.IO.Compression.ZipArchiveEntry entry, string targetDir, string relPath)
    {
        var dest = Path.Combine(targetDir, relPath.Replace('/', Path.DirectorySeparatorChar));
        var destDir = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        entry.ExtractToFile(dest, overwrite: true);
    }

    //TryCopySounds 递归复制源目录所有音频文件到目标目录
    //源目录不存在返回 false 不抛异常
    public static bool TryCopySounds(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            Log.Warning($"Audio directory not found {sourceDir}");
            return false;
        }
        try
        {
            Directory.CreateDirectory(targetDir);
            int count = 0;
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".ogg" && ext != ".mp3" && ext != ".wav" && ext != ".flac") continue;
                var rel = Path.GetRelativePath(sourceDir, file);
                var dest = Path.Combine(targetDir, rel);
                var destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(file, dest, overwrite: true);
                count++;
            }
            Log.Info($"Audio copy finished {count} files {sourceDir} -> {targetDir}");
            return count > 0;
        }
        catch (Exception ex)
        {
            Log.Warning($"Audio copy failed {sourceDir} {ex.Message}");
            return false;
        }
    }

    //IsDirNonEmpty 检测目录存在且包含至少一个文件或子目录用于跳过重复提取
    private static bool IsDirNonEmpty(string path)
        => Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
}
