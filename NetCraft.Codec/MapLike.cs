namespace NetCraft.Codec;

//抽象map视图对应原版com.mojang.serialization.MapLike
//提供按key或string key查找与entries枚举
public interface MapLike<T>
{
    Optional<T> Get(T key);

    Optional<T> Get(string key);

    IEnumerable<Pair<T, T>> Entries();
}
