namespace NetCraft.DataFixer.Optics;

using NetCraft.DataFixer.Util;

//Inj1 injects into the left branch of Either, maps to vanilla com.mojang.datafixers.optics.Inj1
//rewrites the left value F of Either<F,G> to F2, building Either<F2,G>, the right value is unchanged
public sealed class Inj1<F, G, F2> : Prism<Either<F, G>, Either<F2, G>, F, F2>
{
    //singleton cache, shared after type parameter erasure
    internal static readonly Inj1<object, object, object> Instance = new();

    private Inj1() { }

    //match returns Right<F> for the left value and Left<Either<F2,G>> for the right value
    //Map's first parameter handles the Left branch, the second handles the Right branch
    public Either<Either<F2, G>, F> Match(Either<F, G> either)
        => either.Map(f => Either<Either<F2, G>, F>.Right(f), g => Either<Either<F2, G>, F>.Left(Either<F2, G>.Right(g)));

    //build puts F2 into the left branch of Either
    public Either<F2, G> Build(F2 f2) => Either<F2, G>.Left(f2);

    public override string ToString() => "inj1";
}
