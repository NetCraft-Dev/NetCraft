using NetCraft.Codec;
using NetCraft.Registry;
using System.Text;

namespace NetCraft.Network.Component;

//DataComponentPatch 数据组件补丁对应原版 net.minecraft.core.component.DataComponentPatch
//存储 DataComponentType 到 Optional 的映射 present 表示新增值 empty 表示移除
//STREAM_CODEC 编码 positiveCount+negativeCount 前缀 positive 项含 type+value negative 项仅 type
//DELIMITED_STREAM_CODEC 用于不可信来源长度前缀限制
public sealed class DataComponentPatch
{
    //Empty 空补丁单例
    public static readonly DataComponentPatch Empty = new(new Dictionary<object, Optional<object>>());

    //PersistentCodec 持久化编解码 对应原版 DataComponentPatch.CODEC
    public static readonly Codec<DataComponentPatch> PersistentCodec = new DataComponentPatchMapCodec();

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

    //Size 补丁项数
    public int Size => _map.Count;

    //EntrySet 补丁项集合
    public IEnumerable<KeyValuePair<object, Optional<object>>> EntrySet => _map;

    //GetFrom 取补丁覆盖后的最终值 补丁有该 type 时以补丁为准 否则回退 prototype
    //对应原版 DataComponentPatch.get
    public T? GetFrom<T>(DataComponentGetter prototype, DataComponentType<T> type) where T : class
    {
        if (_map.TryGetValue(type, out var value))
            return value.IsPresent ? (T)value.Get() : null;
        return prototype.Get(type);
    }

    //Forget 丢弃匹配的补丁项 对应原版 forget
    public DataComponentPatch Forget(Func<object, bool> test)
    {
        if (IsEmpty) return Empty;
        var copy = new Dictionary<object, Optional<object>>();
        foreach (var kv in _map)
            if (!test(kv.Key)) copy[kv.Key] = kv.Value;
        return copy.Count == 0 ? Empty : new DataComponentPatch(copy);
    }

    //Split 拆成新增映射与移除集合 对应原版 split
    public SplitResult Split()
    {
        if (IsEmpty) return SplitResult.Empty;
        var builder = new DataComponentMapBuilder();
        var removed = new HashSet<object>();
        foreach (var kv in _map)
        {
            if (kv.Value.IsPresent) builder.SetUnchecked(kv.Key, kv.Value.Get());
            else removed.Add(kv.Key);
        }
        return new SplitResult(builder.Build(), removed);
    }

    //NewBuilder 造补丁构造器 对应原版 DataComponentPatch.builder
    public static Builder NewBuilder() => new();

    //判等按补丁内容逐项比 对应原版 equals
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj) || (obj is DataComponentPatch other && MapEquals(other));

    private bool MapEquals(DataComponentPatch other)
    {
        if (other._map.Count != _map.Count) return false;
        foreach (var kv in _map)
        {
            if (!other._map.TryGetValue(kv.Key, out var value)) return false;
            if (kv.Value.IsPresent != value.IsPresent) return false;
            if (kv.Value.IsPresent && !Equals(kv.Value.Get(), value.Get())) return false;
        }
        return true;
    }

    //哈希与判等一致
    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var kv in _map)
            hash += kv.Key.GetHashCode() ^ kv.Value.GetHashCode();
        return hash;
    }

    //移除项带 ! 前缀 对应原版 toString
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        foreach (var kv in _map)
        {
            if (!first) sb.Append(", ");
            first = false;
            if (kv.Value.IsPresent) sb.Append(kv.Key).Append("=>").Append(kv.Value.Get());
            else sb.Append('!').Append(kv.Key);
        }
        sb.Append('}');
        return sb.ToString();
    }

    //SplitResult 补丁拆分结果 对应原版 DataComponentPatch.SplitResult
    public sealed record SplitResult(DataComponentMap Added, IReadOnlySet<object> Removed)
    {
        public static readonly SplitResult Empty = new(DataComponentMap.Empty, new HashSet<object>());
    }

    //Builder 补丁构造器 对应原版 DataComponentPatch.Builder
    public sealed class Builder
    {
        private readonly Dictionary<object, Optional<object>> _map = new();

        //Set 写入或覆盖该项
        public Builder Set<T>(DataComponentType<T> type, T value) where T : class
        {
            _map[type] = Optional<object>.Of(value);
            return this;
        }

        //Remove 标记移除该项
        public Builder Remove<T>(DataComponentType<T> type) where T : class
        {
            _map[type] = Optional<object>.Empty();
            return this;
        }

        //Set 按带类型的组件条目写入
        public Builder Set<T>(TypedDataComponent<T> component) where T : class => Set(component.Type, component.Value);

        //Build 空补丁返回单例
        public DataComponentPatch Build()
            => _map.Count == 0 ? Empty : new DataComponentPatch(new Dictionary<object, Optional<object>>(_map));
    }
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
