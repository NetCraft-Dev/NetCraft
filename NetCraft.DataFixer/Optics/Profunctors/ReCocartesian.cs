namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//ReCocartesian reverse cocartesian profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.ReCocartesian
//provides unleft/unright to restore Either to a single value; the ForgetE family is based on this
public interface ReCocartesian<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : IReCocartesianMu
{
    //unleft restores App2<P,A,B> from App2<P,Either<A,C>,Either<B,C>>, undoing the left operation
    App2<P, A, B> Unleft<A, B, C>(App2<P, Either<A, C>, Either<B, C>> input);

    //unright restores App2<P,A,B> from App2<P,Either<C,A>,Either<C,B>>, undoing the right operation
    App2<P, A, B> Unright<A, B, C>(App2<P, Either<C, A>, Either<C, B>> input);
}
