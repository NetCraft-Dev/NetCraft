using System.Text.Json.Nodes;
using NetCraft.Codec;

namespace NetCraft.Util.EventLog;

//事件日志读取器对应原版JsonEventLogReader
//按行取JSON再走codec解码
public interface JsonEventLogReader<T> : IDisposable
{
    //取下一个事件，读到底返回default
    T? Next();

    //按行读的reader，readLine返回null表示读完
    static JsonEventLogReader<T> Create(Codec<T> codec, Func<string?> readLine, Action? onClose = null)
        => new LineReader(codec, readLine, onClose);

    //默认实现按行解析，一行一个事件
    private sealed class LineReader(Codec<T> codec, Func<string?> readLine, Action? onClose) : JsonEventLogReader<T>
    {
        public T? Next()
        {
            var line = readLine();
            if (line is null)
                return default;
            return codec.Parse(JsonOps.Instance, JsonNode.Parse(line)).GetOrThrow(message => new IOException(message));
        }

        public void Dispose() => onClose?.Invoke();
    }
}
