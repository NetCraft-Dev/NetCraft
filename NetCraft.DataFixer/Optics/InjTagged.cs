namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Util;

//InjTagged tagged injection maps to vanilla com.mojang.datafixers.optics.InjTagged
//matches when the first component of Pair<K,?> equals key, taking the second component A; otherwise returns the original Pair
public sealed class InjTagged<K, A, B> : Prism<Pair<K, object>, Pair<K, object>, A, B>
{
    private readonly K _key;

    public InjTagged(K key) => _key = key;

    //match returns Right<A> when the key matches, otherwise Left<Pair<K,?>>
    public Either<Pair<K, object>, A> Match(Pair<K, object> pair)
        => Equals(_key, pair.First) ? Either<Pair<K, object>, A>.Right((A)pair.Second!) : Either<Pair<K, object>, A>.Left(pair);

    //build constructs a Pair from key and B
    public Pair<K, object> Build(B b) => Pair<K, object>.Of(_key, b!);

    public override string ToString() => "inj[" + _key + "]";

    public override bool Equals(object? obj)
        => obj is InjTagged<K, A, B> other && Equals(_key, other._key);

    public override int GetHashCode() => _key?.GetHashCode() ?? 0;
}
