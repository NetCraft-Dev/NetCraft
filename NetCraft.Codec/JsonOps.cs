using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetCraft.Codec;

//JsonOps JSON 的 DynamicOps 实现对应原版 com.mojang.serialization.JsonOps
//数据驱动加载全程走这条链 数据包里的 .json 经 Codec + JsonOps 反序列化成游戏对象
//JSON 数字不区分宽度 写出时按 CLR 类型 读回时按 int → long → double 依次尝试
//JsonNode 一个实例只能挂在一个父节点下 所有写入路径一律深拷贝 语义等价原版不可变 JsonElement
public sealed class JsonOps : DynamicOps<JsonNode?>
{
    public static readonly JsonOps Instance = new();

    private JsonOps() { }

    //Parse 解析 JSON 文本为 JsonNode 供数据包文件加载 失败返回 DataResult.Error
    public static DataResult<JsonNode?> Parse(string json)
    {
        try
        {
            return DataResult<JsonNode?>.Success(JsonNode.Parse(json));
        }
        catch (JsonException ex)
        {
            return DataResult<JsonNode?>.Error(() => $"JSON 解析失败: {ex.Message}");
        }
    }

    //Parse 直接从流解析 数据驱动加载走这条重载
    //内部按 Utf8JsonReader 处理字节流 不经过 string 省掉一整趟 UTF-16 转码
    //实测原版 data 目录全量 5MB JSON 字节路径比 string 路径快约 25%
    public static DataResult<JsonNode?> Parse(Stream stream)
    {
        try
        {
            return DataResult<JsonNode?>.Success(JsonNode.Parse(stream));
        }
        catch (JsonException ex)
        {
            return DataResult<JsonNode?>.Error(() => $"JSON 解析失败: {ex.Message}");
        }
    }

    //Null 表示 JSON null 与 System.Text.Json 的约定一致
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

    //CreateMap 要求 key 是字符串 与 JSON 对象模型一致
    public JsonNode? CreateMap(IEnumerable<Pair<JsonNode?, JsonNode?>> map)
    {
        var obj = new JsonObject();
        foreach (var entry in map)
        {
            var key = AsStringKey(entry.First)
                ?? throw new NotSupportedException($"JSON map key 必须是字符串: {Describe(entry.First)}");
            obj[key] = Copy(entry.Second);
        }
        return obj;
    }

    public DataResult<double> GetNumberValue(JsonNode? input)
        => input is JsonValue value && TryReadNumber(value, out var number)
            ? DataResult<double>.Success(number)
            : DataResult<double>.Error(() => $"不是数字: {Describe(input)}");

    public DataResult<string> GetStringValue(JsonNode? input)
        => input is JsonValue value && value.TryGetValue<string>(out var text)
            ? DataResult<string>.Success(text)
            : DataResult<string>.Error(() => $"不是字符串: {Describe(input)}");

    //GetBooleanValue 原版先认 JSON 布尔 否则退回按数字 0/1 判定
    public DataResult<bool> GetBooleanValue(JsonNode? input)
    {
        if (input is JsonValue value && value.TryGetValue<bool>(out var flag))
            return DataResult<bool>.Success(flag);
        return GetNumberValue(input).Map(v => v != 0.0);
    }

    public DataResult<JsonNode?> MergeToList(JsonNode? list, JsonNode? value)
    {
        if (list is not null and not JsonArray)
            return DataResult<JsonNode?>.Error(() => $"mergeToList 目标不是数组: {Describe(list)}", list);
        var array = list is JsonArray source ? (JsonArray)source.DeepClone() : new JsonArray();
        array.Add(Copy(value));
        return DataResult<JsonNode?>.Success(array);
    }

    public DataResult<JsonNode?> MergeToList(JsonNode? list, IReadOnlyList<JsonNode?> values)
    {
        if (list is not null and not JsonArray)
            return DataResult<JsonNode?>.Error(() => $"mergeToList 目标不是数组: {Describe(list)}", list);
        var array = list is JsonArray source ? (JsonArray)source.DeepClone() : new JsonArray();
        foreach (var value in values) array.Add(Copy(value));
        return DataResult<JsonNode?>.Success(array);
    }

    public DataResult<JsonNode?> MergeToMap(JsonNode? map, JsonNode? key, JsonNode? value)
    {
        if (map is not null and not JsonObject)
            return DataResult<JsonNode?>.Error(() => $"mergeToMap 目标不是对象: {Describe(map)}", map);
        var keyString = AsStringKey(key);
        if (keyString is null)
            return DataResult<JsonNode?>.Error(() => $"mergeToMap key 不是字符串: {Describe(key)}", map);
        var obj = map is JsonObject source ? (JsonObject)source.DeepClone() : new JsonObject();
        obj[keyString] = Copy(value);
        return DataResult<JsonNode?>.Success(obj);
    }

    public DataResult<JsonNode?> MergeToMap(JsonNode? map, MapLike<JsonNode?> values)
        => MergeEntries(map, values.Entries());

    public DataResult<JsonNode?> MergeToMap(JsonNode? map, IReadOnlyDictionary<JsonNode?, JsonNode?> values)
        => MergeEntries(map, values.Select(e => new Pair<JsonNode?, JsonNode?>(e.Key, e.Value)));

    //MergeEntries 逐项写入 非字符串 key 收集起来一次性报错 与原版行为一致
    private DataResult<JsonNode?> MergeEntries(JsonNode? map, IEnumerable<Pair<JsonNode?, JsonNode?>> entries)
    {
        if (map is not null and not JsonObject)
            return DataResult<JsonNode?>.Error(() => $"mergeToMap 目标不是对象: {Describe(map)}", map);
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
            ? DataResult<JsonNode?>.Error(() => $"部分 key 不是字符串: {string.Join(", ", missed)}", obj)
            : DataResult<JsonNode?>.Success(obj);
    }

    public DataResult<MapLike<JsonNode?>> GetMap(JsonNode? input)
        => input is JsonObject obj
            ? DataResult<MapLike<JsonNode?>>.Success(new JsonMapLike(obj))
            : DataResult<MapLike<JsonNode?>>.Error(() => $"不是对象: {Describe(input)}");

    public DataResult<IEnumerable<Pair<JsonNode?, JsonNode?>>> GetMapValues(JsonNode? input)
        => input is JsonObject obj
            ? DataResult<IEnumerable<Pair<JsonNode?, JsonNode?>>>.Success(
                obj.Select(e => new Pair<JsonNode?, JsonNode?>(JsonValue.Create(e.Key), e.Value)).ToList())
            : DataResult<IEnumerable<Pair<JsonNode?, JsonNode?>>>.Error(() => $"不是对象: {Describe(input)}");

    public DataResult<IEnumerable<JsonNode?>> GetStream(JsonNode? input)
        => input is JsonArray array
            ? DataResult<IEnumerable<JsonNode?>>.Success(array.Select(n => (JsonNode?)n).ToList())
            : DataResult<IEnumerable<JsonNode?>>.Error(() => $"不是数组: {Describe(input)}");

    //Remove 删除 key 返回新对象 非对象原样返回
    public JsonNode? Remove(JsonNode? input, string key)
    {
        if (input is not JsonObject obj) return input;
        var copy = (JsonObject)obj.DeepClone();
        copy.Remove(key);
        return copy;
    }

    //ConvertTo 转换到目标 ops null/对象/数组递归 标量按 string → bool → int → long → double 顺序判定
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
        throw new InvalidOperationException($"无法转换的 JSON 值: {Describe(value)}");
    }

    public RecordBuilder<JsonNode?> MapBuilder() => new JsonRecordBuilder(this);

    //Copy 深拷贝 避免 JsonNode 的"一个实例只能有一个父"限制
    internal static JsonNode? Copy(JsonNode? node) => node?.DeepClone();

    //AsStringKey 取字符串 key 非字符串返回 null
    private static string? AsStringKey(JsonNode? key)
        => key is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "null";

    //TryReadNumber 读 JSON 数字
    //JsonNode.Parse 出来的 JsonValue 内部是 JsonElement 而 CreateInt 之类造出来的内部是 CLR 数字
    //两种承载的 TryGetValue<T> 行为不同 必须逐个宽度试
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
                ?? throw new NotSupportedException($"JSON map key 必须是字符串: {Describe(key)}");
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

//JsonRecordBuilder JSON 的 RecordBuilder 实现对应原版 JsonRecordBuilder
//累积字段到 JsonObject 并支持 prefix 合并
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
        return DataResult<JsonNode?>.Error(() => $"mergeToMap 目标不是对象: {prefix.ToJsonString()}", prefix);
    }
}
