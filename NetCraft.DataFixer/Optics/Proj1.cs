namespace NetCraft.DataFixer.Optics;

using NetCraft.DataFixer.Util;

//Proj1 projects the first component of Pair, maps to vanilla com.mojang.datafixers.optics.Proj1
//view takes Pair.First; update replaces Pair.First and preserves Second
public sealed class Proj1<F, G, F2> : Lens<Pair<F, G>, Pair<F2, G>, F, F2>
{
    //singleton cached, shared after type parameter erasure
    internal static readonly Proj1<object, object, object> Instance = new();

    private Proj1() { }

    public F View(Pair<F, G> pair) => pair.First;
    public Pair<F2, G> Update(F2 newValue, Pair<F, G> pair) => Pair<F2, G>.Of(newValue, pair.Second);

    public override string ToString() => "\u03C01";
}
