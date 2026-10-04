using System.Text;

namespace NetCraft.Storage;

//DirectoryLock 目录锁对应原版 net.minecraft.world.level.storage.DirectoryLock
//用 session.lock 文件做独占防止多进程同时打开同一世界
//跨平台走 FileStream.Lock 托管 API 不依赖平台特定调用
public sealed class DirectoryLock : IDisposable
{
    private const string LockFileName = "session.lock";
    private readonly FileStream _stream;

    private DirectoryLock(FileStream stream)
    {
        _stream = stream;
    }

    //Acquire 在指定目录获取锁
    //目录不存在抛 DirectoryNotFoundException
    //锁已被占用抛 IOException 提示世界正在被其他进程使用
    public static DirectoryLock Acquire(string dir)
    {
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"World directory not found: {dir}");

        var lockPath = Path.Combine(dir, LockFileName);
        var stream = new FileStream(
            lockPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        try
        {
            //写入世界打开时间戳便于外部诊断
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var bytes = Encoding.UTF8.GetBytes(timestamp.ToString());
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
            //独占锁跨平台走托管 Lock API
            stream.Lock(0, stream.Length);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
        return new DirectoryLock(stream);
    }

    public void Dispose()
    {
        try
        {
            if (_stream.Length > 0)
                _stream.Unlock(0, _stream.Length);
        }
        catch
        {
        }
        _stream.Dispose();
    }
}
