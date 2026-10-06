namespace NetCraft.Registry;

//Two-way id/value mapping, maps to vanilla IdMap, implements IEnumerable
public interface IdMap<T> : IEnumerable<T>
{
    //Default id for not found
    public const int Default = -1;

    //Return the id of a value, or -1 if not found
    int GetId(T thing);

    //Look up a value by id, returning default if not found
    T? ById(int id);

    int Size { get; }

    //Look up a value by id, throwing if not found
    T ByIdOrThrow(int id)
    {
        var result = ById(id);
        if (result is null) throw new ArgumentException($"No value with id {id}");
        return result;
    }

    //Look up the id of a value, throwing if not found
    int GetIdOrThrow(T value)
    {
        var id = GetId(value);
        if (id == Default) throw new ArgumentException($"Can't find id for '{value}' in map {this}");
        return id;
    }
}

//Simple IdMap implementation, maps to vanilla IdMapper, auto-assigns incrementing ids
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

    //Add and assign a new id; return the existing id if already present
    public int Add(T value)
    {
        if (_toId.TryGetValue(value, out var existing)) return existing;
        var id = _values.Count + _nextId;
        _toId[value] = id;
        _values.Add(value);
        return id;
    }

    //Add with a specified id
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
