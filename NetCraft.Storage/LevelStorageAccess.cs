using NetCraft.DataFixer;
using NetCraft.Logging;
using NetCraft.Registry;

namespace NetCraft.Storage;

//预定义维度 ResourceKey<Level> 对应原版 net.minecraft.world.level.Level.OVERWORLD/NETHER/END
public static class LevelKeys
{
    public static readonly ResourceKey<Level> OVERWORLD =
        ResourceKey<Level>.Create(Registries.DIMENSION, Identifier.WithDefaultNamespace("overworld"));

    public static readonly ResourceKey<Level> NETHER =
        ResourceKey<Level>.Create(Registries.DIMENSION, Identifier.WithDefaultNamespace("the_nether"));

    public static readonly ResourceKey<Level> END =
        ResourceKey<Level>.Create(Registries.DIMENSION, Identifier.WithDefaultNamespace("the_end"));
}

//LevelStorageAccess 单个世界的存储访问入口
//对应原版 net.minecraft.world.level.storage.LevelStorageSource.LevelStorageAccess
//持有 worldDir 与 DirectoryLock，提供按维度获取路径和创建 SimpleRegionStorage 的能力
//DirectoryLock 防止多进程同时打开同一世界
public sealed class LevelStorageAccess : IDisposable
{
    private readonly string _worldDir;
    private readonly string _worldName;
    //每个维度一个 SimpleRegionStorage 缓存避免重复创建
    private readonly Dictionary<ResourceKey<Level>, SimpleRegionStorage> _dimensionStorages = new();
    //DirectoryLock 独占 session.lock 防止多进程同时操作同一世界
    private readonly DirectoryLock? _directoryLock;
    private bool _disposed;

    public string WorldName => _worldName;
    public string WorldDir => _worldDir;

    //LevelDataPath 世界元数据文件路径对应原版 level.dat
    public string LevelDataPath => Path.Combine(_worldDir, "level.dat");

    //LevelDataOldPath 备份文件路径
    public string LevelDataOldPath => Path.Combine(_worldDir, "level.dat_old");

    //HasLock 指示是否持有目录锁无锁场景下为 false
    public bool HasLock => _directoryLock is not null;

    internal LevelStorageAccess(string worldDir, string worldName, bool acquireLock = true)
    {
        _worldDir = worldDir;
        _worldName = worldName;
        if (acquireLock)
            _directoryLock = DirectoryLock.Acquire(worldDir);
    }

    //GetDimensionPath 获取维度数据目录路径
    //overworld 直接是 worldDir，其他维度在 worldDir/dim_<name>
    public string GetDimensionPath(ResourceKey<Level> levelKey)
    {
        Log.Debug($"GetDimensionPath entry levelKey={levelKey}");
        if (levelKey.Identifier == LevelKeys.OVERWORLD.Identifier)
        {
            Log.Debug($"GetDimensionPath exit result={_worldDir}");
            return _worldDir;
        }
        var result = Path.Combine(_worldDir, "dim_" + levelKey.Identifier.Path);
        Log.Debug($"GetDimensionPath exit result={result}");
        return result;
    }

    //GetRegionPath 获取维度下 region 目录路径
    public string GetRegionPath(ResourceKey<Level> levelKey)
        => Path.Combine(GetDimensionPath(levelKey), "region");

    //CreateRegionStorage 为指定维度创建或复用 SimpleRegionStorage
    //fixer DataFixer 实例用于升级旧版本 chunk
    //dataFixType DataFixTypes 标识升级类型
    public SimpleRegionStorage CreateRegionStorage(
        ResourceKey<Level> levelKey,
        NetCraft.DataFixer.DataFixer fixer,
        DataFixTypes dataFixType,
        bool syncWrites = true)
    {
        Log.Debug($"CreateRegionStorage entry levelKey={levelKey} fixer={fixer} dataFixType={dataFixType} syncWrites={syncWrites}");
        if (_dimensionStorages.TryGetValue(levelKey, out var existing))
        {
            Log.Debug($"CreateRegionStorage exit result={existing}");
            return existing;
        }

        var regionDir = GetRegionPath(levelKey);
        Directory.CreateDirectory(regionDir);
        var info = new RegionStorageInfo(_worldName, levelKey, "chunk");
        var storage = new SimpleRegionStorage(info, regionDir, fixer, syncWrites, dataFixType);
        _dimensionStorages[levelKey] = storage;
        Log.Debug($"CreateRegionStorage exit result={storage}");
        return storage;
    }

    //GetExistingRegionStorage 获取已创建的维度存储不存在返回 null
    public SimpleRegionStorage? GetExistingRegionStorage(ResourceKey<Level> levelKey)
        => _dimensionStorages.TryGetValue(levelKey, out var storage) ? storage : null;

    public void Dispose()
    {
        //Log.Debug($"Dispose 入口");
        if (_disposed)
        {
            //Log.Debug($"Dispose 出口");
            return;
        }
        foreach (var storage in _dimensionStorages.Values)
            storage.Dispose();
        _dimensionStorages.Clear();
        _directoryLock?.Dispose();
        _disposed = true;
        //Log.Debug($"Dispose 出口");
    }
}
