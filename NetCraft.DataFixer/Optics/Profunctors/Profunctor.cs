namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;

//Profunctor二元类型类对应原版com.mojang.datafixers.optics.profunctors.Profunctor
//提供dimap双映射与lmap/rmap单边映射
public interface Profunctor<P, TMu> : Kind2<P, TMu> where P : K2 where TMu : IProfunctorMu
{
    //标记IProfunctorMu链式继承IKind2Mu与K1
    interface Mu : IProfunctorMu { }

    //还原类型应用为Profunctor
    //proofBox实际是FunctionTypeInstance等具体实例实现App<TMu,P>接口
    //C#严格泛型不变量下App<FunctionTypeInstance.Mu,P>与App<IProfunctorMu,P>不同封闭类型强转失败
    //Java类型擦除下App<Proof,P>运行时等同App<Object,Object>任意App实例均可强转
    //用Unsafe.As绕过运行时类型检查对齐Java虚方法分派语义
    static Profunctor<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : IProfunctorMu
    {
        object box = proofBox;
        return System.Runtime.CompilerServices.Unsafe.As<object, Profunctor<P2, TMu2>>(ref box!);
    }

    //dimap返回函数App2<P,A,B>->App2<P,C,D>用g逆映射输入h正映射输出
    Func<App2<P, A, B>, App2<P, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h);

    //dimap直接应用函数到参数
    App2<P, C, D> Dimap<A, B, C, D>(App2<P, A, B> arg, Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h).Invoke(arg);

    //lmap仅映射左输入等价dimap(g,identity)
    App2<P, C, B> Lmap<A, B, C>(App2<P, A, B> input, Func<C, A> g)
        => Dimap<A, B, C, B>(input, g, x => x);

    //rmap仅映射右输出等价dimap(identity,h)
    App2<P, A, D> Rmap<A, B, D>(App2<P, A, B> input, Func<B, D> h)
        => Dimap<A, B, A, D>(input, x => x, h);
}
