using System.Collections.Concurrent;

namespace NetCraft.Util.Collection;

//Memoization helper, maps to vanilla net.minecraft.util.Util.memoize
//Uses ConcurrentDictionary to cache function results, repeated calls hit the cache directly
public static class Memoize
{
    //memoize single-argument function memoization, maps to vanilla Util.memoize(Function)
    //C# uses ConcurrentDictionary.GetOrAdd instead of Java ConcurrentHashMap.computeIfAbsent
    public static Func<T, R> MemoizeFunction<T, R>(Func<T, R> function)
        where T : notnull
    {
        var cache = new ConcurrentDictionary<T, R>();
        return arg => cache.GetOrAdd(arg, function);
    }

    //memoize two-argument function memoization, maps to vanilla Util.memoize(BiFunction)
    //Uses a Pair as key to cache two-argument results
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
