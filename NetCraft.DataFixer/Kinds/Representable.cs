namespace NetCraft.DataFixer.Kinds;

using NetCraft.DataFixer;

//Representable可表示函子对应原版com.mojang.datafixers.kinds.Representable
//提供to/from在容器与Reader函数间的互转FunctionType.ReaderInstance实现此
public interface Representable<T, C, TMu> : Functor<T, TMu> where T : K1 where TMu : IRepresentableMu
{
    //标记IRepresentableMu链式继承IFunctorMu与K1
    interface Mu : IRepresentableMu { }

    static Representable<T2, C2, TMu2> Unbox<T2, C2, TMu2>(App<TMu2, T2> proofBox) where T2 : K1 where TMu2 : IRepresentableMu
        => (Representable<T2, C2, TMu2>)(object)proofBox;

    //转容器到Reader函数空间
    App<FunctionTypes.ReaderMu<C>, A> To<A>(App<T, A> input);
    //从Reader函数空间转回容器
    App<T, A> From<A>(App<FunctionTypes.ReaderMu<C>, A> input);
}
