using System.Text;
using System.Text.Json.Nodes;
using NetCraft.Codec;

namespace NetCraft.Util.EventLog;

//JSON事件日志对应原版JsonEventLog
//一行一条事件的JSON，写入时定位到文件尾追加
public sealed class JsonEventLog<T> : IDisposable
{
    private readonly Codec<T> _codec;

    private readonly FileStream _stream;

    private int _referenceCount = 1;

    public JsonEventLog(Codec<T> codec, FileStream stream)
    {
        _codec = codec;
        _stream = stream;
    }

    //打开或新建日志文件，读写共用一条流
    public static JsonEventLog<T> Open(Codec<T> codec, string path)
        => new(codec, new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));

    //追加一条事件
    public void Write(T value)
    {
        var json = _codec.EncodeStart(JsonOps.Instance, value).GetOrThrow(message => new IOException(message));
        _stream.Position = _stream.Length;
        _stream.Write(Encoding.UTF8.GetBytes((json?.ToJsonString() ?? "null") + "\n"));
        _stream.Flush();
    }

    //开一个读取器，读取器自己持有一次引用
    public JsonEventLogReader<T> OpenReader()
    {
        if (Volatile.Read(ref _referenceCount) <= 0)
            throw new IOException("Event log has already been closed");
        Interlocked.Increment(ref _referenceCount);
        return new TrackingReader(this);
    }

    public void Dispose()
    {
        if (Interlocked.Decrement(ref _referenceCount) <= 0)
            _stream.Dispose();
    }

    //从上次停下的位置接着读，多个读取器互不影响
    private sealed class TrackingReader(JsonEventLog<T> owner) : JsonEventLogReader<T>
    {
        private long _position;

        public T? Next()
        {
            var stream = owner._stream;
            if (_position >= stream.Length)
                return default;
            stream.Position = _position;
            var line = ReadLine(stream);
            _position = stream.Position;
            if (line is null)
                return default;
            return owner._codec.Parse(JsonOps.Instance, JsonNode.Parse(line)).GetOrThrow(message => new IOException(message));
        }

        public void Dispose() => owner.Dispose();
    }

    //读到换行或文件尾，文件尾且没有内容时给null
    private static string? ReadLine(FileStream stream)
    {
        var buffer = new MemoryStream();
        int value;
        while ((value = stream.ReadByte()) >= 0)
        {
            if (value == '\n')
                return Encoding.UTF8.GetString(buffer.ToArray());
            buffer.WriteByte((byte)value);
        }
        return buffer.Length > 0 ? Encoding.UTF8.GetString(buffer.ToArray()) : null;
    }
}
