namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Util;

//InjTagged带标签注入对应原版com.mojang.datafixers.optics.InjTagged
//匹配Pair<K,?>第一分量key相同则取第二分量A否则返回原Pair
public sealed class InjTagged<K, A, B> : Prism<Pair<K, object>, Pair<K, object>, A, B>
{
    private readonly K _key;

    public InjTagged(K key) => _key = key;

    //match键匹配返回Right<A>否则返回Left<Pair<K,?>>
    public Either<Pair<K, object>, A> Match(Pair<K, object> pair)
        => Equals(_key, pair.First) ? Either<Pair<K, object>, A>.Right((A)pair.Second!) : Either<Pair<K, object>, A>.Left(pair);

    //build用key和B构造Pair
    public Pair<K, object> Build(B b) => Pair<K, object>.Of(_key, b!);

    public override string ToString() => "inj[" + _key + "]";

    public override bool Equals(object? obj)
        => obj is InjTagged<K, A, B> other && Equals(_key, other._key);

    public override int GetHashCode() => _key?.GetHashCode() ?? 0;
}
