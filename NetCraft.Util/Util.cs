namespace NetCraft.Util;

//General utility helpers, maps to vanilla net.minecraft.util.Util
//Only Memoize is inlined, pure functions; other methods (WriteAndReadTypedOrThrow etc. that depend on DFU types are placed in DataFixUtils per dependency direction)
//Other methods (getRandomMillis/getMillis, etc.) are implemented as needed in ProfilingUtil
public static class Util
{
    //Thread-safe memoization cache, maps to vanilla Util.memoize
    public static Func<T, R> Memoize<T, R>(Func<T, R> fn) where T : notnull
    {
        var cache = new System.Collections.Concurrent.ConcurrentDictionary<T, R>();
        return input => cache.GetOrAdd(input, fn);
    }
}
