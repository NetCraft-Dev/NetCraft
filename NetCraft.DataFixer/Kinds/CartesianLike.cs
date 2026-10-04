namespace NetCraft.DataFixer.Kinds;

using NetCraft.DataFixer.Util;

//笛卡尔类型类对应原版com.mojang.datafixers.kinds.CartesianLike
public interface CartesianLike<T, C, TMu> : Functor<T, TMu>, Traversable<T, TMu> where T : K1 where TMu : ICartesianLikeMu
{
    //标记ICartesianLikeMu链式继承ITraversableMu与K1
    interface Mu : ICartesianLikeMu { }

    static CartesianLike<T2, C2, TMu2> Unbox<T2, C2, TMu2>(App<TMu2, T2> proofBox) where T2 : K1 where TMu2 : ICartesianLikeMu
        => (CartesianLike<T2, C2, TMu2>)(object)proofBox;

    //转容器到Pair表示
    App<Pairs.Mu<C>, A> To<A>(App<T, A> input);
    //从Pair表示转回容器
    App<T, A> From<A>(App<Pairs.Mu<C>, A> input);
}
