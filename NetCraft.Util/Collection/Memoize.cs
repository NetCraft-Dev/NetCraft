using System.Collections.Concurrent;

namespace NetCraft.Util.Collection;

//记忆化工具对应原版net.minecraft.util.Util.memoize
//用ConcurrentDictionary缓存函数结果重复调用直接命中缓存
public static class Memoize
{
    //memoize单参数函数记忆化对应原版Util.memoize(Function)
    //C#用ConcurrentDictionary.GetOrAdd替代Java ConcurrentHashMap.computeIfAbsent
    public static Func<T, R> MemoizeFunction<T, R>(Func<T, R> function)
        where T : notnull
    {
        var cache = new ConcurrentDictionary<T, R>();
        return arg => cache.GetOrAdd(arg, function);
    }

    //memoize双参数函数记忆化对应原版Util.memoize(BiFunction)
    //用Pair作key缓存双参数结果
    public static Func<T, U, R> MemoizeBiFunction<T, U, R>(Func<T, U, R> function)
        where T : notnull
        where U : notnull
    {
        var cache = new ConcurrentDictionary<KeyValuePair<T, U>, R>();
        return (a, b) =>
        {
            var key = new KeyValuePair<T, U>(a, b);
            return cache.GetOrAdd(key, k => function(k.Key, k.Value));
        };
    }
}
