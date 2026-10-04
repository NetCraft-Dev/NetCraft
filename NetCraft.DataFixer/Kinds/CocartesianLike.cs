namespace NetCraft.DataFixer.Kinds;

using NetCraft.DataFixer.Util;

//共笛卡尔类型类对应原版com.mojang.datafixers.kinds.CocartesianLike
public interface CocartesianLike<T, C, TMu> : Functor<T, TMu>, Traversable<T, TMu> where T : K1 where TMu : ICocartesianLikeMu
{
    //标记ICocartesianLikeMu链式继承ITraversableMu与K1
    interface Mu : ICocartesianLikeMu { }

    static CocartesianLike<T2, C2, TMu2> Unbox<T2, C2, TMu2>(App<TMu2, T2> proofBox) where T2 : K1 where TMu2 : ICocartesianLikeMu
        => (CocartesianLike<T2, C2, TMu2>)(object)proofBox;

    //转容器到Either表示
    App<Eithers.Mu<C>, A> To<A>(App<T, A> input);
    //从Either表示转回容器
    App<T, A> From<A>(App<Eithers.Mu<C>, A> input);
}
