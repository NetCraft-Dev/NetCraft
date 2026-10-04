namespace NetCraft.Registry;

//ID与值双向映射，对应原版IdMap，继承IEnumerable
public interface IdMap<T> : IEnumerable<T>
{
    //未找到的默认ID
    public const int Default = -1;

    //查值的ID找不到返回-1
    int GetId(T thing);

    //按ID查值找不到返回default
    T? ById(int id);

    int Size { get; }

    //按ID查值找不到抛异常
    T ByIdOrThrow(int id)
    {
        var result = ById(id);
        if (result is null) throw new ArgumentException($"No value with id {id}");
        return result;
    }

    //查ID找不到抛异常
    int GetIdOrThrow(T value)
    {
        var id = GetId(value);
        if (id == Default) throw new ArgumentException($"Can't find id for '{value}' in map {this}");
        return id;
    }
}

//IdMap简单实现对应原版IdMapper，自动分配递增ID
public sealed class IdMapper<T> : IdMap<T>
{
    private readonly List<T?> _values = new();
    private readonly Dictionary<T, int> _toId = new();

    public const int DefaultStartId = 0;

    private readonly int _nextId;

    public IdMapper() : this(DefaultStartId) { }

    public IdMapper(int startId)
    {
        _nextId = startId;
    }

    //添加并分配新ID已存在返回已有ID
    public int Add(T value)
    {
        if (_toId.TryGetValue(value, out var existing)) return existing;
        var id = _values.Count + _nextId;
        _toId[value] = id;
        _values.Add(value);
        return id;
    }

    //按指定ID添加
    public void Add(T value, int id)
    {
        while (_values.Count <= id - _nextId)
            _values.Add(default);
        _values[id - _nextId] = value;
        _toId[value] = id;
    }

    public int GetId(T thing) => _toId.TryGetValue(thing, out var id) ? id : IdMap<T>.Default;

    public T? ById(int id)
    {
        var idx = id - _nextId;
        return (uint)idx < (uint)_values.Count ? _values[idx] : default;
    }

    public int Size => _values.Count;

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var v in _values)
            if (v is not null) yield return v;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
