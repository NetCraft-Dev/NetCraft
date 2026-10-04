namespace NetCraft.DataFixer.Optics;

using System.Collections.Generic;
using NetCraft.DataFixer.Kinds;

//ListTraversal列表遍历对应原版com.mojang.datafixers.optics.ListTraversal
//用Applicative.Ap2累积List<B1>结果List<A1>->App<F,List<B1>>
//类型参数名A1/B1避免与方法类型参数A/B同名冲突
public sealed class ListTraversal<A1, B1> : Traversal<List<A1>, List<B1>, A1, B1>
{
    //单例缓存类型参数擦除后共享
    internal static readonly ListTraversal<object, object> Instance = new();

    private ListTraversal() { }

    //wander遍历List<A1>用Applicative累积List<B1>ap2逐元素追加Builder最后返回累积结果
    public Func<List<A1>, App<F, List<B1>>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A1, App<F, B1>> input) where F : K1 where TMu2 : IApplicativeMu
    {
        return list =>
        {
            var builder = new List<B1>();
            App<F, List<B1>> result = applicative.Point<List<B1>>(builder);
            foreach (var a in list)
            {
                App<F, B1> element = input(a);
                App<F, Func<List<B1>, B1, List<B1>>> appendFunc = applicative.Point<Func<List<B1>, B1, List<B1>>>((lst, b) => { lst.Add(b); return lst; });
                result = applicative.Ap2<List<B1>, B1, List<B1>>(appendFunc, result, element);
            }
            return result;
        };
    }

    public override string ToString() => "ListTraversal";
}
