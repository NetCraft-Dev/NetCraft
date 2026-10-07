namespace NetCraft.DataFixer.Optics;

using NetCraft.DataFixer.Util;

//Inj2 injects the Either right branch, maps to vanilla com.mojang.datafixers.optics.Inj2
//rewrites the right value G of Either<F,G> to G2, building Either<F,G2> with the left value unchanged
public sealed class Inj2<F, G, G2> : Prism<Either<F, G>, Either<F, G2>, G, G2>
{
    //singleton cached, shared after type parameter erasure
    internal static readonly Inj2<object, object, object> Instance = new();

    private Inj2() { }

    //match returns Right<G> for a right value and Left<Either<F,G2>> for a left value
    public Either<Either<F, G2>, G> Match(Either<F, G> either)
        => either.Map(f => Either<Either<F, G2>, G>.Left(Either<F, G2>.Left(f)), g => Either<Either<F, G2>, G>.Right(g));

    //build puts G2 into the Either right branch
    public Either<F, G2> Build(G2 g2) => Either<F, G2>.Right(g2);

    public override string ToString() => "inj2";
}
