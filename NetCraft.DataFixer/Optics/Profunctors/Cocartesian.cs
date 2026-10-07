namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//Cocartesian profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.Cocartesian
//provides left/right operations on Either; Prism is based on this
public interface Cocartesian<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : ICocartesianMu
{
    //recover the type application as Cocartesian
    static Cocartesian<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : ICocartesianMu
        => (Cocartesian<P2, TMu2>)(object)proofBox;

    //left extends A->B to Either<A,C>->Either<B,C>, attaching the C branch
    App2<P, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<P, A, B> input);

    //right extends A->B to Either<C,A>->Either<C,B>, attaching the C branch; implemented via swap to left
    //Dimap type parameters are explicit to avoid C# lambda inference failure
    App2<P, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<P, A, B> input)
        => Dimap<Either<A, C>, Either<B, C>, Either<C, A>, Either<C, B>>(
            Left<A, B, C>(input),
            e => e.Swap(),
            e => e.Swap()
        );
}
