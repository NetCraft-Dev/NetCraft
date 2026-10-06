using System.Text;
using System.Text.Json.Nodes;
using NetCraft.Codec;

namespace NetCraft.Util.EventLog;

//JSON event log, maps to vanilla JsonEventLog
//JSON with one event per line, seeks to the end of the file on write to append
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

    //Opens or creates the log file, sharing one stream for read and write
    public static JsonEventLog<T> Open(Codec<T> codec, string path)
        => new(codec, new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));

    //Appends one event
    public void Write(T value)
    {
        var json = _codec.EncodeStart(JsonOps.Instance, value).GetOrThrow(message => new IOException(message));
        _stream.Position = _stream.Length;
        _stream.Write(Encoding.UTF8.GetBytes((json?.ToJsonString() ?? "null") + "\n"));
        _stream.Flush();
    }

    //Opens a reader, the reader holds its own reference
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

    //Continues reading from where it last stopped, multiple readers do not interfere
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

    //Reads until newline or end of file, returns null when at end with no content
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
