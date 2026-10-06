namespace NetCraft.Util.Collection;

//Single-key cache, maps to vanilla net.minecraft.util.SingleKeyCache
//Caches the most recent computed key-value pair, the next same key returns the cached value directly
public sealed class SingleKeyCache<K, V>
    where K : class
{
    private readonly Func<K, V> _computeValue;
    private K? _cacheKey;
    private V? _cachedValue;
    private bool _hasValue;

    public SingleKeyCache(Func<K, V> computeValue)
    {
        _computeValue = computeValue;
    }

    //getValue reads the cache by key, recomputes on a miss or key change, maps to vanilla getValue
    //Vanilla uses cachedValue==null to detect an empty value; C# uses a _hasValue flag to distinguish null from uncomputed
    //Key comparison uses EqualityComparer, aligning with the value-equality semantics of vanilla Objects.equals
    public V GetValue(K cacheKey)
    {
        if (!_hasValue || !EqualityComparer<K>.Default.Equals(_cacheKey, cacheKey))
        {
            _cachedValue = _computeValue(cacheKey);
            _cacheKey = cacheKey;
            _hasValue = true;
        }
        return _cachedValue!;
    }
}
