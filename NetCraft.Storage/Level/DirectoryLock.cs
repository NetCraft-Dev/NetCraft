using System.Text;

namespace NetCraft.Storage;

//DirectoryLock, directory lock, maps to vanilla net.minecraft.world.level.storage.DirectoryLock
//Uses a session.lock file for exclusivity, preventing multiple processes opening the same world
//Portably uses the managed FileStream.Lock API, no platform-specific calls
public sealed class DirectoryLock : IDisposable
{
    private const string LockFileName = "session.lock";
    private readonly FileStream _stream;

    private DirectoryLock(FileStream stream)
    {
        _stream = stream;
    }

    //Acquire takes the lock in the given directory
    //Throws DirectoryNotFoundException when the directory is missing
    //Throws IOException when the lock is taken, meaning the world is in use by another process
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
            //Write the world-open timestamp for external diagnostics
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var bytes = Encoding.UTF8.GetBytes(timestamp.ToString());
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
            //The exclusive lock uses the managed Lock API for portability
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
