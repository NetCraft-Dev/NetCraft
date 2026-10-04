namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//Cocartesian共笛卡尔profunctor对应原版com.mojang.datafixers.optics.profunctors.Cocartesian
//提供left/right对Either的操作Prism基于此
public interface Cocartesian<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : ICocartesianMu
{
    //还原类型应用为Cocartesian
    static Cocartesian<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : ICocartesianMu
        => (Cocartesian<P2, TMu2>)(object)proofBox;

    //left把A->B扩展为Either<A,C>->Either<B,C>附加C分支
    App2<P, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<P, A, B> input);

    //right把A->B扩展为Either<C,A>->Either<C,B>附加C分支用swap转left实现
    //Dimap类型参数显式指定避免C#lambda推断失败
    App2<P, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<P, A, B> input)
        => Dimap<Either<A, C>, Either<B, C>, Either<C, A>, Either<C, B>>(
            Left<A, B, C>(input),
            e => e.Swap(),
            e => e.Swap()
        );
}
