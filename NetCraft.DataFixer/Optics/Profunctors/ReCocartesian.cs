namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//ReCocartesian逆共笛卡尔profunctor对应原版com.mojang.datafixers.optics.profunctors.ReCocartesian
//提供unleft/unright从Either还原到单值ForgetE系列基于此
public interface ReCocartesian<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : IReCocartesianMu
{
    //unleft从App2<P,Either<A,C>,Either<B,C>>还原App2<P,A,B>撤销left操作
    App2<P, A, B> Unleft<A, B, C>(App2<P, Either<A, C>, Either<B, C>> input);

    //unright从App2<P,Either<C,A>,Either<C,B>>还原App2<P,A,B>撤销right操作
    App2<P, A, B> Unright<A, B, C>(App2<P, Either<C, A>, Either<C, B>> input);
}
