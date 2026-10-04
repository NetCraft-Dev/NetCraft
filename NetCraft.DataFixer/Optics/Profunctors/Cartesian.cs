namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//Cartesian笛卡尔profunctor对应原版com.mojang.datafixers.optics.profunctors.Cartesian
//提供first/second对Pair的操作Lens基于此
public interface Cartesian<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : ICartesianMu
{
    //还原类型应用为Cartesian
    //用Unsafe.As绕过TMu类型参数不匹配对齐Java类型擦除语义
    //proofBox运行时实现Cartesian<P,具体Mu>但Unbox期望Cartesian<P,ICartesianMu>接口
    static Cartesian<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : ICartesianMu
    {
        var obj = (object)proofBox;
        return System.Runtime.CompilerServices.Unsafe.As<object, Cartesian<P2, TMu2>>(ref obj);
    }

    //first把A->B扩展为Pair<A,C>->Pair<B,C>附加C分量
    App2<P, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<P, A, B> input);

    //second把A->B扩展为Pair<C,A>->Pair<C,B>附加C分量用swap转first实现
    //Dimap类型参数显式指定避免C#lambda推断失败
    App2<P, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<P, A, B> input)
        => Dimap<Pair<A, C>, Pair<B, C>, Pair<C, A>, Pair<C, B>>(
            First<A, B, C>(input),
            p => p.Swap(),
            p => p.Swap()
        );
}
