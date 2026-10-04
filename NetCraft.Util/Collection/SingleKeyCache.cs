namespace NetCraft.Util.Collection;

//单键缓存对应原版net.minecraft.util.SingleKeyCache
//缓存最近一次计算的键值对下次同key直接返回缓存值
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

    //getValue按key取缓存未命中或key变化时重新计算对应原版getValue
    //原版用cachedValue==null判断空值C#用_hasValue标志区分null与未计算
    //key比较用EqualityComparer对齐原版Objects.equals值相等语义
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
