namespace NetCraft.DataFixer.Kinds;

//可遍历类型类提供Traverse与Flip
public interface Traversable<TT, TMu> : Functor<TT, TMu> where TT : K1 where TMu : ITraversableMu
{
    //标记ITraversableMu链式继承IFunctorMu与K1
    interface Mu : ITraversableMu { }

    static Traversable<TT2, TMu2> Unbox<TT2, TMu2>(App<TMu2, TT2> proofBox) where TT2 : K1 where TMu2 : ITraversableMu
        => (Traversable<TT2, TMu2>)(object)proofBox;

    //遍历容器元素用Applicative累积结果
    App<TF2, App<TT, B>> Traverse<TF2, TMu2, A, B>(Applicative<TF2, TMu2> applicative, Func<A, App<TF2, B>> function, App<TT, A> input) where TF2 : K1 where TMu2 : IApplicativeMu;

    //翻转嵌套容器App<T,App<F,A>>到App<F,App<T,A>>
    App<TF2, App<TT, A>> Flip<TF2, TMu2, A>(Applicative<TF2, TMu2> applicative, App<TT, App<TF2, A>> input) where TF2 : K1 where TMu2 : IApplicativeMu
        => Traverse<TF2, TMu2, App<TF2, A>, A>(applicative, x => x, input);
}
