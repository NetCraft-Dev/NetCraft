namespace NetCraft.DataFixer.Kinds;

//函子类型类提供Map操作
public interface Functor<TF, TMu> : Kind1<TF, TMu> where TF : K1 where TMu : IFunctorMu
{
    //标记IFunctorMu链式继承IKind1Mu与K1
    interface Mu : IFunctorMu { }

    static Functor<TF2, TMu2> Unbox<TF2, TMu2>(App<TMu2, TF2> proofBox) where TF2 : K1 where TMu2 : IFunctorMu
        => (Functor<TF2, TMu2>)(object)proofBox;

    //映射函数到容器内元素
    App<TF, R> Map<T, R>(Func<T, R> func, App<TF, T> ts);
}
