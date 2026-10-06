using System.Collections;
using System.Runtime.CompilerServices;
using NetCraft.Registry;

namespace NetCraft.Storage.Paletted;

//Simplified int identity hash bidirectional map, maps to vanilla CrudeIncrementalIntIdentityHashBiMap
//The keys and values arrays store by hash, byId stores by id, giving bidirectional id-to-value and value-to-id lookup
//Vanilla Java uses identityHashCode + reference comparison; C# uses GetHashCode + EqualityComparer to support struct K
//C# unconstrained generic K? does not produce Nullable<K> for struct K, so _occupied arrays flag slot occupancy instead of an is null check
internal sealed class CrudeIncrementalIntIdentityHashBiMap<K> : IdMap<K>
{
    private const int NotFound = -1;
    private const float LoadFactor = 0.8f;
    private static readonly IEqualityComparer<K> Comparer = EqualityComparer<K>.Default;

    private K[] _keys;
    private int[] _values;
    private K[] _byId;
    private bool[] _byIdOccupied;
    private bool[] _keysOccupied;
    private int _nextId;
    private int _size;

    private CrudeIncrementalIntIdentityHashBiMap(int capacity)
    {
        _keys = new K[capacity];
        _values = new int[capacity];
        _byId = new K[capacity];
        _byIdOccupied = new bool[capacity];
        _keysOccupied = new bool[capacity];
    }

    private CrudeIncrementalIntIdentityHashBiMap(
        K[] keys, int[] values, K[] byId, bool[] byIdOccupied, bool[] keysOccupied, int nextId, int size)
    {
        _keys = keys;
        _values = values;
        _byId = byId;
        _byIdOccupied = byIdOccupied;
        _keysOccupied = keysOccupied;
        _nextId = nextId;
        _size = size;
    }

    public static CrudeIncrementalIntIdentityHashBiMap<A> Create<A>(int initialCapacity)
        => new((int)(initialCapacity / LoadFactor));

    public int GetId(K thing) => GetValue(IndexOf(thing, Hash(thing)));

    public K? ById(int id)
    {
        if (id < 0 || id >= _byId.Length || !_byIdOccupied[id]) return default;
        return _byId[id];
    }

    private int GetValue(int index) => index == -1 ? -1 : _values[index];

    public bool Contains(K key) => GetId(key) != -1;

    public bool Contains(int id) => id >= 0 && id < _byIdOccupied.Length && _byIdOccupied[id];

    public int Add(K key)
    {
        var value = NextId();
        AddMapping(key, value);
        return value;
    }

    private int NextId()
    {
        while (_nextId < _byIdOccupied.Length && _byIdOccupied[_nextId])
            _nextId++;
        return _nextId;
    }

    private void Grow(int newSize)
    {
        var oldKeys = _keys;
        var oldValues = _values;
        var oldOccupied = _keysOccupied;
        var resized = new CrudeIncrementalIntIdentityHashBiMap<K>(newSize);
        for (var i = 0; i < oldKeys.Length; i++)
        {
            if (oldOccupied[i]) resized.AddMapping(oldKeys[i], oldValues[i]);
        }
        _keys = resized._keys;
        _values = resized._values;
        _byId = resized._byId;
        _byIdOccupied = resized._byIdOccupied;
        _keysOccupied = resized._keysOccupied;
        _nextId = resized._nextId;
        _size = resized._size;
    }

    public void AddMapping(K key, int id)
    {
        var minSize = Math.Max(id, _size + 1);
        if (minSize >= _keys.Length * LoadFactor)
        {
            var length = _keys.Length;
            int newSize;
            while (true)
            {
                newSize = length << 1;
                if (newSize >= id) break;
                length = newSize;
            }
            Grow(newSize);
        }
        var index = FindEmpty(Hash(key));
        _keys[index] = key;
        _values[index] = id;
        _keysOccupied[index] = true;
        if (id >= _byId.Length)
        {
            var newLen = _byId.Length;
            while (newLen <= id) newLen <<= 1;
            Array.Resize(ref _byId, newLen);
            Array.Resize(ref _byIdOccupied, newLen);
        }
        _byId[id] = key;
        _byIdOccupied[id] = true;
        _size++;
        if (id == _nextId) _nextId++;
    }

    //Vanilla uses identityHashCode, which for a class is object identity; C# uses GetHashCode to support struct K hashed by value
    private int Hash(K key)
        => (MurmurHash3Mixer(Comparer.GetHashCode(key)) & int.MaxValue) % _keys.Length;

    //Maps to vanilla Mth.murmurHash3Mixer, spreading the hash distribution to avoid clustering
    private static int MurmurHash3Mixer(int hash)
    {
        var hash2 = (hash ^ (hash >>> 16)) * -2048144789;
        var hash3 = (hash2 ^ (hash2 >>> 13)) * -1028477387;
        return hash3 ^ (hash3 >>> 16);
    }

    private int IndexOf(K key, int startFrom)
    {
        for (var i = startFrom; i < _keys.Length; i++)
        {
            if (!_keysOccupied[i]) return -1;
            if (Comparer.Equals(_keys[i], key)) return i;
        }
        for (var i = 0; i < startFrom; i++)
        {
            if (!_keysOccupied[i]) return -1;
            if (Comparer.Equals(_keys[i], key)) return i;
        }
        return -1;
    }

    private int FindEmpty(int startFrom)
    {
        for (var i = startFrom; i < _keys.Length; i++)
        {
            if (!_keysOccupied[i]) return i;
        }
        for (var i = 0; i < startFrom; i++)
        {
            if (!_keysOccupied[i]) return i;
        }
        throw new InvalidOperationException("Overflowed :(");
    }

    public void Clear()
    {
        Array.Clear(_keys, 0, _keys.Length);
        Array.Clear(_byId, 0, _byId.Length);
        Array.Clear(_byIdOccupied, 0, _byIdOccupied.Length);
        Array.Clear(_keysOccupied, 0, _keysOccupied.Length);
        _nextId = 0;
        _size = 0;
    }

    public int Size => _size;

    public IEnumerator<K> GetEnumerator()
    {
        for (var i = 0; i < _byIdOccupied.Length; i++)
            if (_byIdOccupied[i]) yield return _byId[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public CrudeIncrementalIntIdentityHashBiMap<K> Copy()
        => new((K[])_keys.Clone(), (int[])_values.Clone(), (K[])_byId.Clone(),
            (bool[])_byIdOccupied.Clone(), (bool[])_keysOccupied.Clone(), _nextId, _size);
}
