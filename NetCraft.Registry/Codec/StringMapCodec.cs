using NetCraft.Codec;

namespace NetCraft.Registry.Codec;

//string map codec对应原版Codec.unboundedMap(Codec.STRING, Codec.STRING)
//序列化为CompoundTag每个entry为key到string映射
public sealed class StringMapCodec : AbstractMapCodec<Dictionary<string, string>>
{
    public static readonly StringMapCodec Instance = new();

    public override DataResult<Dictionary<string, string>> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var dict = new Dictionary<string, string>();
        foreach (var (key, value) in input.Entries())
        {
            var keyResult = ops.GetStringValue(key);
            if (!keyResult.Result().IsPresent) continue;
            var valueResult = ops.GetStringValue(value);
            if (!valueResult.Result().IsPresent) continue;
            dict[keyResult.GetOrThrow()] = valueResult.GetOrThrow();
        }
        return DataResult<Dictionary<string, string>>.Success(dict);
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, Dictionary<string, string> value, RecordBuilder<U> builder)
    {
        foreach (var (k, v) in value)
            builder.Add(k, ops.CreateString(v));
        return builder;
    }
}
