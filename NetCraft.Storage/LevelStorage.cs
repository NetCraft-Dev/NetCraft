using NetCraft.DataFixer;
using NetCraft.Registry;

namespace NetCraft.Storage;

//LevelStorage 世界存储入口对应原版 net.minecraft.world.level.storage.LevelStorageSource
//按世界名管理 LevelStorageAccess 实例，世界根目录 baseDir 下每个子目录一个世界
//简化版不含 DirectoryLock 原版用文件锁防止多进程同时操作
public sealed class LevelStorage
{
    private readonly string _baseDir;

    //BaseDir 世界根目录默认 worlds
    public string BaseDir => _baseDir;

    public LevelStorage(string baseDir)
    {
        _baseDir = baseDir;
    }

    //CreateAccess 创建或加载世界存储访问入口
    //worldName 世界名对应 baseDir 下子目录名
    //acquireLock 是否获取 DirectoryLock 默认 true 测试场景传 false 避免独占冲突
    public LevelStorageAccess CreateAccess(string worldName, bool acquireLock = true)
    {
        var worldDir = Path.Combine(_baseDir, worldName);
        Directory.CreateDirectory(worldDir);
        return new LevelStorageAccess(worldDir, worldName, acquireLock);
    }

    //ListWorlds 列出 baseDir 下所有世界目录名
    public IEnumerable<string> ListWorlds()
    {
        if (!Directory.Exists(_baseDir)) return Enumerable.Empty<string>();
        return Directory.EnumerateDirectories(_baseDir)
            .Select(Path.GetFileName!)
            .Where(name => name is not null);
    }

    //WorldExists 判断指定世界是否存在
    public bool WorldExists(string worldName)
        => Directory.Exists(Path.Combine(_baseDir, worldName));

    //DeleteWorld 删除指定世界目录及其所有内容
    public void DeleteWorld(string worldName)
    {
        var worldDir = Path.Combine(_baseDir, worldName);
        if (Directory.Exists(worldDir))
            Directory.Delete(worldDir, recursive: true);
    }
}
