using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Network.Component;

//DataComponentPatchMapCodec persistence codec for component patches, maps to vanilla DataComponentPatch.CODEC
//The key is the component registry name, a ! prefix means removal, and the value is the coding result of the component's own Codec
internal sealed class DataComponentPatchMapCodec : AbstractMapCodec<DataComponentPatch>
{
    private const string RemovedPrefix = "!";

    public override DataResult<DataComponentPatch> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var map = new Dictionary<object, Optional<object>>();
        foreach (var entry in input.Entries())
        {
            var keyText = ops.GetStringValue(entry.First);
            if (!keyText.Result().IsPresent) return DataResult<DataComponentPatch>.Error(() => "component patch keys must be strings");
            var text = keyText.GetOrThrow();
            var removed = text.StartsWith(RemovedPrefix, StringComparison.Ordinal);
            var idText = removed ? text[RemovedPrefix.Length..] : text;
            var id = Identifier.TryParse(idText);
            if (id is null) return DataResult<DataComponentPatch>.Error(() => $"unrecognized component name {idText}");
            var holder = BuiltInRegistries.DATA_COMPONENT_TYPE.Get(id.Value);
            if (holder?.Value is not DataComponentType<object> type)
                return DataResult<DataComponentPatch>.Error(() => $"component not yet registered {idText}");
            if (removed)
            {
                map[type] = Optional<object>.Empty();
                continue;
            }
            var parsed = type.CodecOrThrow().Parse(ops, entry.Second);
            if (!parsed.Result().IsPresent) return DataResult<DataComponentPatch>.Error(() => $"failed to parse component {idText}");
            map[type] = Optional<object>.Of(parsed.GetOrThrow());
        }
        return DataResult<DataComponentPatch>.Success(map.Count == 0 ? DataComponentPatch.Empty : new DataComponentPatch(map));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, DataComponentPatch value, RecordBuilder<U> builder)
    {
        foreach (var pair in value.AsMap())
        {
            if (pair.Key is not DataComponentType<object> type) continue;
            if (type.IsTransient) continue;
            var name = BuiltInRegistries.DATA_COMPONENT_TYPE.GetKey(type)?.ToString();
            if (name is null) continue;
            if (pair.Value.IsPresent)
            {
                var encoded = type.CodecOrThrow().EncodeStart(ops, pair.Value.Get());
                if (!encoded.Result().IsPresent) continue;
                builder.Add(name, encoded.GetOrThrow());
            }
            else
            {
                builder.Add(RemovedPrefix + name, ops.Empty());
            }
        }
        return builder;
    }
}
