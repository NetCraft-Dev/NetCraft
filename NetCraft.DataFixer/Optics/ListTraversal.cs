namespace NetCraft.DataFixer.Optics;

using System.Collections.Generic;
using NetCraft.DataFixer.Kinds;

//ListTraversal list traversal maps to vanilla com.mojang.datafixers.optics.ListTraversal
//accumulates List<B1> results via Applicative.Ap2, List<A1>->App<F,List<B1>>
//type parameter names A1/B1 avoid conflicting with the method type parameters A/B
public sealed class ListTraversal<A1, B1> : Traversal<List<A1>, List<B1>, A1, B1>
{
    //singleton cached, shared after type parameter erasure
    internal static readonly ListTraversal<object, object> Instance = new();

    private ListTraversal() { }

    //wander traverses List<A1>, accumulating List<B1> via Applicative: ap2 appends each element to a Builder and returns the accumulated result
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
