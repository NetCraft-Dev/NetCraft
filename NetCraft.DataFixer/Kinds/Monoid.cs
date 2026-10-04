namespace NetCraft.DataFixer.Kinds;

using System.Collections.Generic;

//Monoid类型类对应原版com.mojang.datafixers.kinds.Monoid
public interface Monoid<T>
{
    //单位元
    T Point();
    //二元合并
    T Add(T first, T second);

    //listMonoid列表拼接Monoid
    static Monoid<List<T>> ListMonoid<T>() => Monoids.ListMonoid<T>();
}

//Monoids非泛型静态工具类避免泛型接口Monoid<T>静态方法调用歧义
public static class Monoids
{
    //listMonoid列表拼接Monoid
    public static Monoid<List<T>> ListMonoid<T>() => new ListMonoidImpl<T>();

    //ListMonoidImpl列表拼接实现
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
