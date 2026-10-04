namespace NetCraft.Util.Parsing.Packrat;

//带缓存的ParseState基类对应原版net.minecraft.util.parsing.packrat.CachedParseState
//按位置缓存NamedRule解析结果避免重复解析
//具体子类提供mark/restore/input实现
public abstract class CachedParseState<S> : ParseState<S>
{
    private readonly ErrorCollector<S> _errorCollector;
    private int _nextControlToReturn;
    private PositionCache[] _positionCache = new PositionCache[256];
    private readonly Scope _scope = new();
    private SimpleControl[] _controlCache = new SimpleControl[16];
    private readonly SilentParseState _silent;

    protected CachedParseState(ErrorCollector<S> errorCollector)
    {
        _errorCollector = errorCollector;
        _silent = new SilentParseState(this);
    }

    public Scope Scope => _scope;
    public ErrorCollector<S> ErrorCollector => _errorCollector;
    public ParseState<S> Silent => _silent;

    //子类必须实现mark/restore/input
    public abstract S Input { get; }
    public abstract int Mark();
    public abstract void Restore(int mark);

    public T Parse<T>(NamedRule<S, T> rule)
    {
        var markBeforeParse = Mark();
        var positionCache = GetCacheForPosition(markBeforeParse);
        var entryIndex = positionCache.FindKeyIndex(rule.Name);
        if (entryIndex != -1)
        {
            var value = positionCache.GetValue<T>(entryIndex);
            if (value is not null)
            {
                if (ReferenceEquals(value, CacheEntry<T>.Negative)) return default!;
                Restore(value.MarkAfterParse);
                return value.Value!;
            }
        }
        else
        {
            entryIndex = positionCache.AllocateNewEntry(rule.Name);
        }
        var result = rule.Value.Parse(this);
        CacheEntry<T> cacheEntry;
        if (result is null)
        {
            cacheEntry = CacheEntry<T>.NegativeEntry();
        }
        else
        {
            var markAfterParse = Mark();
            cacheEntry = new CacheEntry<T>(result, markAfterParse);
        }
        positionCache.SetValue(entryIndex, cacheEntry);
        return result!;
    }


    public Control AcquireControl()
    {
        var currentSize = _controlCache.Length;
        if (_nextControlToReturn >= currentSize)
        {
            var newSize = GrowByHalf(currentSize, _nextControlToReturn + 1);
            var newControlCache = new SimpleControl[newSize];
            Array.Copy(_controlCache, newControlCache, currentSize);
            _controlCache = newControlCache;
        }
        var controlIndex = _nextControlToReturn++;
        var entry = _controlCache[controlIndex];
        if (entry is null)
        {
            entry = new SimpleControl();
            _controlCache[controlIndex] = entry;
        }
        else
        {
            entry.Reset();
        }
        return entry;
    }

    public void ReleaseControl() => _nextControlToReturn--;

    private PositionCache GetCacheForPosition(int index)
    {
        var currentSize = _positionCache.Length;
        if (index >= currentSize)
        {
            var newSize = GrowByHalf(currentSize, index + 1);
            var newCache = new PositionCache[newSize];
            Array.Copy(_positionCache, newCache, currentSize);
            _positionCache = newCache;
        }
        var result = _positionCache[index];
        if (result is null)
        {
            result = new PositionCache();
            _positionCache[index] = result;
        }
        return result;
    }

    private static int GrowByHalf(int current, int needed)
        => Math.Max(current + (current >> 1), needed);

    private sealed class PositionCache
    {
        private object?[] _atomCache = new object?[16];
        private int _nextKey;

        public int FindKeyIndex(Atom? key)
        {
            for (var i = 0; i < _nextKey; i += 2)
            {
                if (_atomCache[i] == key) return i;
            }
            return -1;
        }

        public int AllocateNewEntry(Atom? key)
        {
            var newKeyIndex = _nextKey;
            _nextKey += 2;
            var newValueIndex = newKeyIndex + 1;
            var currentSize = _atomCache.Length;
            if (newValueIndex >= currentSize)
            {
                var newSize = GrowByHalf(currentSize, newValueIndex + 1);
                var newCache = new object?[newSize];
                Array.Copy(_atomCache, newCache, currentSize);
                _atomCache = newCache;
            }
            _atomCache[newKeyIndex] = key;
            return newKeyIndex;
        }

        private static int GrowByHalf(int current, int needed)
            => Math.Max(current + (current >> 1), needed);

        public CacheEntry<T>? GetValue<T>(int keyIndex)
            => (CacheEntry<T>?)_atomCache[keyIndex + 1];

        public void SetValue(int keyIndex, object? entry)
            => _atomCache[keyIndex + 1] = entry;
    }

    private sealed class CacheEntry<T>
    {
        public T? Value { get; }
        public int MarkAfterParse { get; }
        public static CacheEntry<T> Negative { get; } = new(default, -1);

        public CacheEntry(T? value, int markAfterParse)
        {
            Value = value;
            MarkAfterParse = markAfterParse;
        }

        public static CacheEntry<T> NegativeEntry() => Negative;
    }

    private sealed class SimpleControl : Control
    {
        private bool _hasCut;

        public void Cut() => _hasCut = true;

        public bool HasCut() => _hasCut;

        public void Reset() => _hasCut = false;
    }

    private sealed class SilentParseState : ParseState<S>
    {
        private readonly CachedParseState<S> _this;
        private readonly ErrorCollector<S> _silentCollector;

        public SilentParseState(CachedParseState<S> owner)
        {
            _this = owner;
            _silentCollector = NopErrorCollector<S>.Instance;
        }

        public Scope Scope => _this.Scope;
        public ErrorCollector<S> ErrorCollector => _silentCollector;
        public ParseState<S> Silent => this;
        public S Input => _this.Input;

        public T Parse<T>(NamedRule<S, T> rule) => _this.Parse(rule);
        public int Mark() => _this.Mark();
        public void Restore(int mark) => _this.Restore(mark);
        public Control AcquireControl() => _this.AcquireControl();
        public void ReleaseControl() => _this.ReleaseControl();
    }
}
