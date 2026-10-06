using System.Text.Json.Nodes;
using NetCraft.Codec;

namespace NetCraft.Util.EventLog;

//Event log reader, maps to vanilla JsonEventLogReader
//Reads JSON by line then decodes via the codec
public interface JsonEventLogReader<T> : IDisposable
{
    //Reads the next event, returns default at end
    T? Next();

    //Line-based reader, readLine returning null means done
    static JsonEventLogReader<T> Create(Codec<T> codec, Func<string?> readLine, Action? onClose = null)
        => new LineReader(codec, readLine, onClose);

    //Default implementation parses by line, one event per line
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
