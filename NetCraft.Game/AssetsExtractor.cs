using System.IO.Compression;
using System.Linq;
using NetCraft.Logging;

namespace NetCraft.Game;

//AssetsExtractor asset helper functions
//Extracts assets from the jar path given on the command line and the downloaded sounds directory into the program root's assets
//The jar only extracts entries with the assets/ subdirectory prefix, strips the prefix and releases them into root/assets, preserving the vanilla resource layout
//Core business does not depend on this class; it loads directly from ExtractedJarDir/ExtractedSoundsDir for decoupling
//Startup arguments: --jar-path gives the jar file path, --sounds-dir gives the sounds directory
public static class AssetsExtractor
{
    //ExtractedJarDir fixed directory where business loads jar resources: root/assets
    public const string ExtractedJarDir = "assets";

    //ExtractedSoundsDir fixed directory where business loads sound resources: root/assets/sounds
    public const string ExtractedSoundsDir = "assets/sounds";

    //Extract reads the path arguments from GameOptions and extracts the jar and sounds into the fixed directory
    //Returns true when at least one item extracted successfully; false when none were configured or all failed
    public static bool Extract(GameOptions options)
    {
        Log.SetClassSource(typeof(AssetsExtractor));

        bool any = false;

        var jarPath = options.GetOptionOrDefault("jar-path", string.Empty);
        if (!string.IsNullOrEmpty(jarPath))
        {
            //Skip extraction when the assets directory exists and is non-empty, to avoid re-extracting on every startup
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
            //Skip copying when the sounds directory exists and is non-empty, to avoid re-copying on every startup
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

    //TryExtractJar: the old signature delegates to the new one with dataDir=null rootDir=null, kept for backward compatibility
    //Only extracts the assets/ prefix, no data/; pack.mcmeta is copied to the old assetsDir location
    public static bool TryExtractJar(string jarPath, string targetDir)
        => TryExtractJar(jarPath, targetDir, null, null);

    //TryExtractJar extracts entries with the assets/ and data/ prefixes from the jar into their respective target directories
    //assets/ goes stripped into assetsDir, keeping the assets/<ns>/... substructure
    //data/ goes stripped into dataDir, keeping the data/<ns>/... substructure; skipped when dataDir is null
    //The root pack.mcmeta is copied next to assets/data in rootDir for FolderPackResources to read
    //Falls back to assetsDir when rootDir is null, for backward compatibility
    //Skips irrelevant entries such as class/META-INF; returns false without throwing when the jar is missing or the path is invalid
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
                //Some jars (decompiled packages) prefix entries with resources/; strip it before matching pack.mcmeta/assets/data
                var entryPath = fullName.StartsWith("resources/", StringComparison.OrdinalIgnoreCase)
                    ? fullName.Substring("resources/".Length)
                    : fullName;
                //The root pack.mcmeta is copied next to assets/data in rootDir for FolderPackResources to read
                //Falls back to assetsDir when rootDir is null, for backward compatibility
                if (entryPath.Equals("pack.mcmeta", StringComparison.OrdinalIgnoreCase))
                {
                    var dest = Path.Combine(rootDir ?? assetsDir, "pack.mcmeta");
                    entry.ExtractToFile(dest, overwrite: true);
                    packMcmetaCopied = true;
                    continue;
                }
                //assets/ prefix stripped into assetsDir
                if (entryPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = entryPath.Substring("assets/".Length);
                    ExtractEntry(entry, assetsDir, rel);
                    assetsCount++;
                    continue;
                }
                //data/ prefix stripped into dataDir; skipped when dataDir is null
                if (dataDir is not null && entryPath.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = entryPath.Substring("data/".Length);
                    ExtractEntry(entry, dataDir, rel);
                    dataCount++;
                    continue;
                }
                //Skips irrelevant entries such as class/META-INF
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

    //ExtractEntry releases a zip entry into the target directory, preserving the relative substructure
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

    //TryCopySounds recursively copies all sound files from the source directory to the target directory
    //Returns false without throwing when the source directory is missing
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

    //IsDirNonEmpty checks that the directory exists and contains at least one file or subdirectory, used to skip repeated extraction
    private static bool IsDirNonEmpty(string path)
        => Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
}
