namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//ReCartesian逆笛卡尔profunctor对应原版com.mojang.datafixers.optics.profunctors.ReCartesian
//提供unfirst/unsecond从Pair还原到单值Forget系列基于此
public interface ReCartesian<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : IReCartesianMu
{
    //unfirst从App2<P,Pair<A,C>,Pair<B,C>>还原App2<P,A,B>撤销first操作
    App2<P, A, B> Unfirst<A, B, C>(App2<P, Pair<A, C>, Pair<B, C>> input);

    //unsecond从App2<P,Pair<C,A>,Pair<C,B>>还原App2<P,A,B>撤销second操作
    App2<P, A, B> Unsecond<A, B, C>(App2<P, Pair<C, A>, Pair<C, B>> input);
}
