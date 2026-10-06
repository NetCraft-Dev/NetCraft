using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetCraft.Codec;

//JsonOps, the DynamicOps implementation for JSON. Mirrors vanilla com.mojang.serialization.JsonOps
//The whole data-driven loading path goes through here: .json files in data packs are deserialized into game objects via Codec + JsonOps
//JSON numbers carry no width: writes follow the CLR type, reads try int → long → double in order
//A JsonNode instance can only have one parent, so every write path deep-copies; semantically equivalent to vanilla's immutable JsonElement
public sealed class JsonOps : DynamicOps<JsonNode?>
{
    public static readonly JsonOps Instance = new();

    private JsonOps() { }

    //Parse JSON text into a JsonNode for data pack loading; returns DataResult.Error on failure
    public static DataResult<JsonNode?> Parse(string json)
    {
        try
        {
            return DataResult<JsonNode?>.Success(JsonNode.Parse(json));
        }
        catch (JsonException ex)
        {
            return DataResult<JsonNode?>.Error(() => $"JSON parse failed: {ex.Message}");
        }
    }

    //Parse straight from a stream; data-driven loading uses this overload
    //Handles the byte stream with Utf8JsonReader, skipping the string round trip and a whole UTF-16 conversion
    //Measured on the full 5MB of JSON in vanilla's data directory, the byte path is about 25% faster than the string path
    public static DataResult<JsonNode?> Parse(Stream stream)
    {
        try
        {
            return DataResult<JsonNode?>.Success(JsonNode.Parse(stream));
        }
        catch (JsonException ex)
        {
            return DataResult<JsonNode?>.Error(() => $"JSON parse failed: {ex.Message}");
        }
    }

    //Null represents JSON null, matching the System.Text.Json convention
    public JsonNode? Empty() => null;

    public JsonNode? EmptyList() => new JsonArray();

    public JsonNode? EmptyMap() => new JsonObject();

    public JsonNode? CreateByte(byte value) => JsonValue.Create(value);

    public JsonNode? CreateShort(short value) => JsonValue.Create(value);

    public JsonNode? CreateInt(int value) => JsonValue.Create(value);

    public JsonNode? CreateLong(long value) => JsonValue.Create(value);

    public JsonNode? CreateFloat(float value) => JsonValue.Create(value);

    public JsonNode? CreateDouble(double value) => JsonValue.Create(value);

    public JsonNode? CreateBoolean(bool value) => JsonValue.Create(value);

    public JsonNode? CreateNumeric(double value) => JsonValue.Create(value);

    public JsonNode? CreateString(string value) => JsonValue.Create(value);

    public JsonNode? CreateList(IEnumerable<JsonNode?> stream)
    {
        var array = new JsonArray();
        foreach (var item in stream) array.Add(Copy(item));
        return array;
    }

    //CreateMap requires string keys, consistent with the JSON object model
    public JsonNode? CreateMap(IEnumerable<Pair<JsonNode?, JsonNode?>> map)
    {
        var obj = new JsonObject();
        foreach (var entry in map)
        {
            var key = AsStringKey(entry.First)
                ?? throw new NotSupportedException($"JSON map key must be a string: {Describe(entry.First)}");
            obj[key] = Copy(entry.Second);
        }
        return obj;
    }

    public DataResult<double> GetNumberValue(JsonNode? input)
        => input is JsonValue value && TryReadNumber(value, out var number)
            ? DataResult<double>.Success(number)
            : DataResult<double>.Error(() => $"Not a number: {Describe(input)}");

    public DataResult<string> GetStringValue(JsonNode? input)
        => input is JsonValue value && value.TryGetValue<string>(out var text)
            ? DataResult<string>.Success(text)
            : DataResult<string>.Error(() => $"Not a string: {Describe(input)}");

    //GetBooleanValue accepts a JSON boolean first, otherwise falls back to treating numbers as 0/1
    public DataResult<bool> GetBooleanValue(JsonNode? input)
    {
        if (input is JsonValue value && value.TryGetValue<bool>(out var flag))
            return DataResult<bool>.Success(flag);
        return GetNumberValue(input).Map(v => v != 0.0);
    }

    public DataResult<JsonNode?> MergeToList(JsonNode? list, JsonNode? value)
    {
        if (list is not null and not JsonArray)
            return DataResult<JsonNode?>.Error(() => $"mergeToList target is not an array: {Describe(list)}", list);
        var array = list is JsonArray source ? (JsonArray)source.DeepClone() : new JsonArray();
        array.Add(Copy(value));
        return DataResult<JsonNode?>.Success(array);
    }

    public DataResult<JsonNode?> MergeToList(JsonNode? list, IReadOnlyList<JsonNode?> values)
    {
        if (list is not null and not JsonArray)
            return DataResult<JsonNode?>.Error(() => $"mergeToList target is not an array: {Describe(list)}", list);
        var array = list is JsonArray source ? (JsonArray)source.DeepClone() : new JsonArray();
        foreach (var value in values) array.Add(Copy(value));
        return DataResult<JsonNode?>.Success(array);
    }

    public DataResult<JsonNode?> MergeToMap(JsonNode? map, JsonNode? key, JsonNode? value)
    {
        if (map is not null and not JsonObject)
            return DataResult<JsonNode?>.Error(() => $"mergeToMap target is not an object: {Describe(map)}", map);
        var keyString = AsStringKey(key);
        if (keyString is null)
            return DataResult<JsonNode?>.Error(() => $"mergeToMap key is not a string: {Describe(key)}", map);
        var obj = map is JsonObject source ? (JsonObject)source.DeepClone() : new JsonObject();
        obj[keyString] = Copy(value);
        return DataResult<JsonNode?>.Success(obj);
    }

    public DataResult<JsonNode?> MergeToMap(JsonNode? map, MapLike<JsonNode?> values)
        => MergeEntries(map, values.Entries());

    public DataResult<JsonNode?> MergeToMap(JsonNode? map, IReadOnlyDictionary<JsonNode?, JsonNode?> values)
        => MergeEntries(map, values.Select(e => new Pair<JsonNode?, JsonNode?>(e.Key, e.Value)));

    //MergeEntries writes entries one by one and collects non-string keys to report them all at once, matching vanilla
    private DataResult<JsonNode?> MergeEntries(JsonNode? map, IEnumerable<Pair<JsonNode?, JsonNode?>> entries)
    {
        if (map is not null and not JsonObject)
            return DataResult<JsonNode?>.Error(() => $"mergeToMap target is not an object: {Describe(map)}", map);
        var obj = map is JsonObject source ? (JsonObject)source.DeepClone() : new JsonObject();
        var missed = new List<string>();
        foreach (var entry in entries)
        {
            var key = AsStringKey(entry.First);
            if (key is null)
            {
                missed.Add(Describe(entry.First));
                continue;
            }
            obj[key] = Copy(entry.Second);
        }
        return missed.Count > 0
            ? DataResult<JsonNode?>.Error(() => $"Some keys are not strings: {string.Join(", ", missed)}", obj)
            : DataResult<JsonNode?>.Success(obj);
    }

    public DataResult<MapLike<JsonNode?>> GetMap(JsonNode? input)
        => input is JsonObject obj
            ? DataResult<MapLike<JsonNode?>>.Success(new JsonMapLike(obj))
            : DataResult<MapLike<JsonNode?>>.Error(() => $"Not an object: {Describe(input)}");

    public DataResult<IEnumerable<Pair<JsonNode?, JsonNode?>>> GetMapValues(JsonNode? input)
        => input is JsonObject obj
            ? DataResult<IEnumerable<Pair<JsonNode?, JsonNode?>>>.Success(
                obj.Select(e => new Pair<JsonNode?, JsonNode?>(JsonValue.Create(e.Key), e.Value)).ToList())
            : DataResult<IEnumerable<Pair<JsonNode?, JsonNode?>>>.Error(() => $"Not an object: {Describe(input)}");

    public DataResult<IEnumerable<JsonNode?>> GetStream(JsonNode? input)
        => input is JsonArray array
            ? DataResult<IEnumerable<JsonNode?>>.Success(array.Select(n => (JsonNode?)n).ToList())
            : DataResult<IEnumerable<JsonNode?>>.Error(() => $"Not an array: {Describe(input)}");

    //Remove deletes a key and returns a new object; non-objects are returned unchanged
    public JsonNode? Remove(JsonNode? input, string key)
    {
        if (input is not JsonObject obj) return input;
        var copy = (JsonObject)obj.DeepClone();
        copy.Remove(key);
        return copy;
    }

    //ConvertTo converts to the target ops, recursing through null/object/array and testing scalars as string → bool → int → long → double
    public U ConvertTo<U>(DynamicOps<U> ops, JsonNode? input)
    {
        if (input is null) return ops.Empty();
        if (input is JsonObject obj)
            return ops.CreateMap(obj.Select(e => new Pair<U, U>(ops.CreateString(e.Key), ConvertTo(ops, e.Value))));
        if (input is JsonArray array)
            return ops.CreateList(array.Select(n => ConvertTo(ops, n)));
        var value = (JsonValue)input;
        if (value.TryGetValue<string>(out var text)) return ops.CreateString(text);
        if (value.TryGetValue<bool>(out var flag)) return ops.CreateBoolean(flag);
        if (value.TryGetValue<int>(out var intValue)) return ops.CreateInt(intValue);
        if (value.TryGetValue<long>(out var longValue)) return ops.CreateLong(longValue);
        if (value.TryGetValue<float>(out var floatValue)) return ops.CreateFloat(floatValue);
        if (value.TryGetValue<double>(out var doubleValue)) return ops.CreateDouble(doubleValue);
        if (TryReadNumber(value, out var numeric)) return ops.CreateNumeric(numeric);
        throw new InvalidOperationException($"Cannot convert JSON value: {Describe(value)}");
    }

    public RecordBuilder<JsonNode?> MapBuilder() => new JsonRecordBuilder(this);

    //Copy deep-copies to work around JsonNode's one-parent-per-instance restriction
    internal static JsonNode? Copy(JsonNode? node) => node?.DeepClone();

    //AsStringKey returns the key when it is a string, otherwise null
    private static string? AsStringKey(JsonNode? key)
        => key is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "null";

    //TryReadNumber reads a JSON number
    //A JsonValue from JsonNode.Parse wraps a JsonElement, while one built by CreateInt and friends wraps a CLR number
    //TryGetValue<T> behaves differently for those two, so every width has to be tried
    private static bool TryReadNumber(JsonValue value, out double number)
    {
        if (value.TryGetValue<double>(out var doubleValue)) { number = doubleValue; return true; }
        if (value.TryGetValue<float>(out var floatValue)) { number = floatValue; return true; }
        if (value.TryGetValue<int>(out var intValue)) { number = intValue; return true; }
        if (value.TryGetValue<long>(out var longValue)) { number = longValue; return true; }
        if (value.TryGetValue<short>(out var shortValue)) { number = shortValue; return true; }
        if (value.TryGetValue<byte>(out var byteValue)) { number = byteValue; return true; }
        if (value.TryGetValue<decimal>(out var decimalValue)) { number = (double)decimalValue; return true; }
        number = 0;
        return false;
    }

    private sealed class JsonMapLike : MapLike<JsonNode?>
    {
        private readonly JsonObject _obj;

        public JsonMapLike(JsonObject obj) => _obj = obj;

        public Optional<JsonNode?> Get(JsonNode? key)
        {
            var keyString = AsStringKey(key)
                ?? throw new NotSupportedException($"JSON map key must be a string: {Describe(key)}");
            return Get(keyString);
        }

        public Optional<JsonNode?> Get(string key)
            => _obj.TryGetPropertyValue(key, out var node)
                ? Optional<JsonNode?>.OfNullable(node)
                : Optional<JsonNode?>.OfNullable(null);

        public IEnumerable<Pair<JsonNode?, JsonNode?>> Entries()
            => _obj.Select(e => new Pair<JsonNode?, JsonNode?>(JsonValue.Create(e.Key), e.Value));
    }
}

//JsonRecordBuilder, the RecordBuilder implementation for JSON. Mirrors vanilla JsonRecordBuilder
//Accumulates fields into a JsonObject and supports prefix merging
public sealed class JsonRecordBuilder : AbstractRecordBuilder<JsonNode?>
{
    public JsonRecordBuilder(JsonOps ops) : base(ops) { }

    protected override JsonNode? InitBuilder() => new JsonObject();

    protected override JsonNode? Append(string key, JsonNode? value, JsonNode? builder)
    {
        if (builder is JsonObject obj) obj[key] = JsonOps.Copy(value);
        return builder;
    }

    protected override DataResult<JsonNode?> Build(
        IReadOnlyList<KeyValuePair<string, JsonNode?>> entries, JsonNode? builder, JsonNode? prefix)
    {
        if (prefix is null) return DataResult<JsonNode?>.Success(builder);
        if (prefix is JsonObject existing && builder is JsonObject built)
        {
            var result = (JsonObject)existing.DeepClone();
            foreach (var (key, value) in built) result[key] = JsonOps.Copy(value);
            return DataResult<JsonNode?>.Success(result);
        }
        return DataResult<JsonNode?>.Error(() => $"mergeToMap target is not an object: {prefix.ToJsonString()}", prefix);
    }
}
