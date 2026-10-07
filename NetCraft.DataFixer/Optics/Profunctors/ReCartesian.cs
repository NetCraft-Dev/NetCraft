namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//ReCartesian reverse Cartesian profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.ReCartesian
//provides unfirst/unsecond to restore a Pair to a single value; the Forget family is based on this
public interface ReCartesian<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : IReCartesianMu
{
    //unfirst restores App2<P,A,B> from App2<P,Pair<A,C>,Pair<B,C>>, undoing the first operation
    App2<P, A, B> Unfirst<A, B, C>(App2<P, Pair<A, C>, Pair<B, C>> input);

    //unsecond restores App2<P,A,B> from App2<P,Pair<C,A>,Pair<C,B>>, undoing the second operation
    App2<P, A, B> Unsecond<A, B, C>(App2<P, Pair<C, A>, Pair<C, B>> input);
}
