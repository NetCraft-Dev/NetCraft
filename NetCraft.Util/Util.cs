namespace NetCraft.Util;

//通用工具集合对应原版net.minecraft.util.Util
//仅内联Memoize纯函数其他方法（WriteAndReadTypedOrThrow等依赖DFU类型放到DataFixUtils中按依赖方向）
//其余方法（getRandomMillis/getMillis等）按需在ProfilingUtil中实现
public static class Util
{
    //线程安全记忆化缓存对应原版Util.memoize
    public static Func<T, R> Memoize<T, R>(Func<T, R> fn) where T : notnull
    {
        var cache = new System.Collections.Concurrent.ConcurrentDictionary<T, R>();
        return input => cache.GetOrAdd(input, fn);
    }
}
