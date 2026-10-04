namespace NetCraft.Registry;

//SynchedValue 一条实体元数据 对应原版 SynchedEntityData.DataValue
//SerializerId 是 Game 层 EntityDataSerializers 的注册序号 Registry 层只透传不解释
public readonly record struct SynchedValue(byte Index, int SerializerId, object Value);

//SynchedEntityData 实体元数据容器 对应原版 net.minecraft.network.syncher.SynchedEntityData
//子类构造时 Define 声明条目 之后 Set 改值 Version 自增
//追踪器记录上次下发时的版本 版本不同就重发全部条目 省去增量与全量的两套路径
public sealed class SynchedEntityData
{
    //按索引有序存放 下发顺序与原版按索引升序一致
    private readonly SortedDictionary<byte, SynchedValue> _values = new();

    //Version 值变化计数 观察者比对它判断有没有变化
    public int Version { get; private set; }

    //Define 声明条目 对应原版 define
    public void Define(byte index, int serializerId, object value)
    {
        _values[index] = new SynchedValue(index, serializerId, value);
        Version++;
    }

    //Get 取条目值 未声明说明索引写错直接抛 避免静默拿到错值
    public object Get(byte index)
        => _values.TryGetValue(index, out var value)
            ? value.Value
            : throw new InvalidOperationException($"实体元数据 {index} 未声明");

    //Set 改条目值 值没变不计数 对应原版 set 只在有差异时标脏
    public void Set(byte index, object value)
    {
        if (!_values.TryGetValue(index, out var current))
            throw new InvalidOperationException($"实体元数据 {index} 未声明");
        if (Equals(current.Value, value)) return;
        _values[index] = current with { Value = value };
        Version++;
    }

    //CollectAll 按索引升序取全部条目
    public List<SynchedValue> CollectAll()
    {
        var result = new List<SynchedValue>(_values.Count);
        foreach (var value in _values.Values) result.Add(value);
        return result;
    }
}

//ISyncedEntity 带同步元数据的实体
//追踪器只认这个接口 不再按 ServerPlayer/ItemEntity 之类的具体类型逐个特判
public interface ISyncedEntity
{
    //SyncedData 实体元数据容器
    SynchedEntityData SyncedData { get; }
}
