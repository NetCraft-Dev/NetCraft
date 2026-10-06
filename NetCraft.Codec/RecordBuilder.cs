namespace NetCraft.Codec;

//Record builder interface, mirroring vanilla com.mojang.serialization.RecordBuilder
//Builds a compound map in field order
public interface RecordBuilder<T>
{
    DynamicOps<T> Ops { get; }

    RecordBuilder<T> Add(string key, T value);

    RecordBuilder<T> Add(string key, T value, T prefix);

    DataResult<T> Build(T prefix);
}

//Abstract base class providing the default Add accumulation and Build
//Subclasses only implement InitBuilder and Append
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

    //Build the final result from the accumulated builder and the prefix merge
    protected abstract DataResult<T> Build(IReadOnlyList<KeyValuePair<string, T>> entries, T builder, T prefix);
}
