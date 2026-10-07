namespace NetCraft.DataFixer.Optics;

using NetCraft.DataFixer.Util;

//Proj2 projects the second component of Pair, maps to vanilla com.mojang.datafixers.optics.Proj2
//view takes Pair.Second; update replaces Pair.Second and preserves First
public sealed class Proj2<F, G, G2> : Lens<Pair<F, G>, Pair<F, G2>, G, G2>
{
    //singleton cached, shared after type parameter erasure
    internal static readonly Proj2<object, object, object> Instance = new();

    private Proj2() { }

    public G View(Pair<F, G> pair) => pair.Second;
    public Pair<F, G2> Update(G2 newValue, Pair<F, G> pair) => Pair<F, G2>.Of(pair.First, newValue);

    public override string ToString() => "\u03C02";
}
