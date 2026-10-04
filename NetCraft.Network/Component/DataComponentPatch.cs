using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Network.Component;

//DataComponentPatch 数据组件补丁对应原版 net.minecraft.core.component.DataComponentPatch
//存储 DataComponentType 到 Optional 的映射 present 表示新增值 empty 表示移除
//STREAM_CODEC 编码 positiveCount+negativeCount 前缀 positive 项含 type+value negative 项仅 type
//DELIMITED_STREAM_CODEC 用于不可信来源长度前缀限制
public sealed class DataComponentPatch
{
    //Empty 空补丁单例
    public static readonly DataComponentPatch Empty = new(new Dictionary<object, Optional<object>>());

    //StreamCodec 网络同步编解码
    public static readonly StreamCodec<RegistryFriendlyByteBuf, DataComponentPatch> StreamCodec
        = new DataComponentPatchStreamCodec();

    //DelimitedStreamCodec 不可信来源编解码含长度前缀限制
    public static readonly StreamCodec<RegistryFriendlyByteBuf, DataComponentPatch> DelimitedStreamCodec
        = new DataComponentPatchStreamCodec();

    private readonly Dictionary<object, Optional<object>> _map;

    public DataComponentPatch(Dictionary<object, Optional<object>> map)
    {
        _map = map;
    }

    //IsEmpty 是否为空补丁
    public bool IsEmpty => _map.Count == 0;

    //Get 按 type 取 Optional present 表示新增值 empty 表示移除 不存在返回 null
    public Optional<object>? Get<T>(DataComponentType<T> type) where T : class
        => _map.TryGetValue(type, out var value) ? value : null;

    //AsMap 返回内部映射副本
    public IReadOnlyDictionary<object, Optional<object>> AsMap() => _map.ToDictionary(kv => kv.Key, kv => kv.Value);
}

//DataComponentPatchStreamCodec DataComponentPatch 网络编解码实现
internal sealed class DataComponentPatchStreamCodec : StreamCodec<RegistryFriendlyByteBuf, DataComponentPatch>
{
    public DataComponentPatch Decode(RegistryFriendlyByteBuf buf)
    {
        int positiveCount = buf.ReadVarInt();
        int negativeCount = buf.ReadVarInt();
        if (positiveCount == 0 && negativeCount == 0)
            return DataComponentPatch.Empty;

        int expectedSize = positiveCount + negativeCount;
        var map = new Dictionary<object, Optional<object>>(Math.Min(expectedSize, ByteBufCodecs.MaxInitialCollectionSize));
        for (int i = 0; i < positiveCount; i++)
        {
            var type = DataComponentTypeCodecs.Decode(buf);
            var codec = (IDataComponentTypeCodec)type;
            var value = codec.DecodeValue(buf);
            map[type] = Optional<object>.Of(value);
        }
        for (int i = 0; i < negativeCount; i++)
        {
            var type = DataComponentTypeCodecs.Decode(buf);
            map[type] = Optional<object>.Empty();
        }
        return new DataComponentPatch(map);
    }

    public void Encode(RegistryFriendlyByteBuf buf, DataComponentPatch value)
    {
        int positiveCount = 0;
        int negativeCount = 0;
        foreach (var kv in value.AsMap())
        {
            if (kv.Value.IsPresent) positiveCount++;
            else negativeCount++;
        }
        buf.WriteVarInt(positiveCount);
        buf.WriteVarInt(negativeCount);
        foreach (var kv in value.AsMap())
        {
            if (kv.Value.IsPresent)
            {
                DataComponentTypeCodecs.Encode(buf, kv.Key);
                ((IDataComponentTypeCodec)kv.Key).EncodeValue(buf, kv.Value.Get());
            }
        }
        foreach (var kv in value.AsMap())
        {
            if (!kv.Value.IsPresent)
            {
                DataComponentTypeCodecs.Encode(buf, kv.Key);
            }
        }
    }
}

//DataComponentTypeCodecs DataComponentType id 编解码工具
//encode 时 type 实例查 registry GetId 写 VarInt decode 时读 id 从 registry 取实例
public static class DataComponentTypeCodecs
{
    //Encode 按 type 实例查 id 写 VarInt
    public static void Encode(RegistryFriendlyByteBuf buf, object type)
    {
        var registry = buf.Lookup(Registries.DATA_COMPONENT_TYPE);
        int id = registry.GetId(type);
        if (id == IdMap<object>.Default)
            throw new InvalidOperationException($"DataComponentType 未注册: {type}");
        buf.WriteVarInt(id);
    }

    //Decode 读 VarInt id 从 registry 取 DataComponentType 实例
    public static object Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        var registry = buf.Lookup(Registries.DATA_COMPONENT_TYPE);
        var type = registry.ById(id);
        if (type is null)
            throw new InvalidOperationException($"未知 DataComponentType id {id}");
        return type;
    }
}
