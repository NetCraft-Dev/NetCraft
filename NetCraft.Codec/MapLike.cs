namespace NetCraft.Codec;

//Abstract map view, mirroring vanilla com.mojang.serialization.MapLike
//Provides lookup by key or string key plus entry enumeration
public interface MapLike<T>
{
    Optional<T> Get(T key);

    Optional<T> Get(string key);

    IEnumerable<Pair<T, T>> Entries();
}
