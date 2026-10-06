using NetCraft.DataFixer;
using NetCraft.Registry;

namespace NetCraft.Storage;

//LevelStorage, world storage entry point, maps to vanilla net.minecraft.world.level.storage.LevelStorageSource
//Manages LevelStorageAccess instances by world name; under the root baseDir each subdirectory is one world
//The simplified version omits DirectoryLock; vanilla uses a file lock to prevent multiple processes operating at once
public sealed class LevelStorage
{
    private readonly string _baseDir;

    //BaseDir, the world root, defaults to worlds
    public string BaseDir => _baseDir;

    public LevelStorage(string baseDir)
    {
        _baseDir = baseDir;
    }

    //CreateAccess creates or loads the world storage access
    //worldName, the world name, matching a subdirectory under baseDir
    //acquireLock, whether to acquire DirectoryLock, default true; test scenarios pass false to avoid an exclusive conflict
    public LevelStorageAccess CreateAccess(string worldName, bool acquireLock = true)
    {
        var worldDir = Path.Combine(_baseDir, worldName);
        Directory.CreateDirectory(worldDir);
        return new LevelStorageAccess(worldDir, worldName, acquireLock);
    }

    //ListWorlds lists all world directory names under baseDir
    public IEnumerable<string> ListWorlds()
    {
        if (!Directory.Exists(_baseDir)) return Enumerable.Empty<string>();
        return Directory.EnumerateDirectories(_baseDir)
            .Select(Path.GetFileName!)
            .Where(name => name is not null);
    }

    //WorldExists reports whether the given world exists
    public bool WorldExists(string worldName)
        => Directory.Exists(Path.Combine(_baseDir, worldName));

    //DeleteWorld deletes the given world directory and all its contents
    public void DeleteWorld(string worldName)
    {
        var worldDir = Path.Combine(_baseDir, worldName);
        if (Directory.Exists(worldDir))
            Directory.Delete(worldDir, recursive: true);
    }
}
