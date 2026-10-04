namespace NetCraft.Codec;

//record builder接口对应原版com.mojang.serialization.RecordBuilder
//按字段顺序构建复合map
public interface RecordBuilder<T>
{
    DynamicOps<T> Ops { get; }

    RecordBuilder<T> Add(string key, T value);

    RecordBuilder<T> Add(string key, T value, T prefix);

    DataResult<T> Build(T prefix);
}

//抽象基类提供Add累积与Build默认实现
//子类实现InitBuilder和Append即可
public abstract class AbstractRecordBuilder<T> : RecordBuilder<T>
{
    private readonly List<KeyValuePair<string, T>> _entries = new();

    protected AbstractRecordBuilder(DynamicOps<T> ops) { Ops = ops; }

    public DynamicOps<T> Ops { get; }

    public RecordBuilder<T> Add(string key, T value)
    {
        _entries.Add(new(key, value));
        return this;
    }

    public RecordBuilder<T> Add(string key, T value, T prefix)
    {
        _entries.Add(new(key, value));
        return this;
    }

    public DataResult<T> Build(T prefix)
    {
        var builder = InitBuilder();
        foreach (var (key, value) in _entries)
            builder = Append(key, value, builder);
        return Build(_entries, builder, prefix);
    }

    protected abstract T InitBuilder();

    protected abstract T Append(string key, T value, T builder);

    //用累积好的builder和prefix合并构建最终结果
    protected abstract DataResult<T> Build(IReadOnlyList<KeyValuePair<string, T>> entries, T builder, T prefix);
}
