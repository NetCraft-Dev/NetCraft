namespace NetCraft.DataFixer.Kinds;

using System.Collections.Generic;

//Monoid typeclass maps to vanilla com.mojang.datafixers.kinds.Monoid
public interface Monoid<T>
{
    //identity element
    T Point();
    //binary combine
    T Add(T first, T second);

    //listMonoid: list concatenation Monoid
    static Monoid<List<T>> ListMonoid<T>() => Monoids.ListMonoid<T>();
}

//Monoids is a non-generic static helper avoiding ambiguity when calling static methods on the generic interface Monoid<T>
public static class Monoids
{
    //listMonoid: list concatenation Monoid
    public static Monoid<List<T>> ListMonoid<T>() => new ListMonoidImpl<T>();

    //ListMonoidImpl: list concatenation implementation
    private sealed class ListMonoidImpl<T> : Monoid<List<T>>
    {
        public List<T> Point() => new();
        public List<T> Add(List<T> first, List<T> second)
        {
            var result = new List<T>(first.Count + second.Count);
            result.AddRange(first);
            result.AddRange(second);
            return result;
        }
    }
}
