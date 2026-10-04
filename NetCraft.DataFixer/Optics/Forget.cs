namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Forgets容器存放Mu标记避免泛型嵌套
public static class Forgets
{
    //二元HKT标记R为求值结果类型
    public sealed class Mu<R> : K2 { }

    //还原类型应用为Forget<R,A,B>
    public static Forget<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (Forget<R, A, B>)(object)box!;
}

//Forget遗忘光学对应原版com.mojang.datafixers.optics.Forget
//求值器A->R忽略B类型参数用于读取只读optic求值
public interface Forget<R, A, B> : App2<Forgets.Mu<R>, A, B>
{
    //run接收A返回R忽略B
    R Run(A a);
}

//Forget具体实现持有Func<A,R>委托
internal sealed class ForgetImpl<R, A, B> : Forget<R, A, B>
{
    private readonly Func<A, R> _function;
    internal ForgetImpl(Func<A, R> function) => _function = function;
    public R Run(A a) => _function(a);
}

//ForgetInstance作为Cartesian+ReCocartesian实例
//用forget工厂方法构造新Forget包装dimap/first/second/unleft/unright组合
public sealed class ForgetInstance<R> : Cartesian<Forgets.Mu<R>, ForgetInstance<R>.Mu>, ReCocartesian<Forgets.Mu<R>, ForgetInstance<R>.Mu>, App<ForgetInstance<R>.Mu, Forgets.Mu<R>>
{
    public sealed class Mu : ICartesianMu, IReCocartesianMu { }
    public static readonly ForgetInstance<R> InstanceOf = new();
    private ForgetInstance() { }

    //dimap用g逆映射输入c->a后调用原Forget.run取R
    public Func<App2<Forgets.Mu<R>, A, B>, App2<Forgets.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => Optics.Forget<R, C, D>(c => Forgets.Unbox<R, A, B>(input).Run(g(c)));

    //first扩展到Pair<A,C>取First分量调用原Forget.run
    public App2<Forgets.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<Forgets.Mu<R>, A, B> input)
        => Optics.Forget<R, Pair<A, C>, Pair<B, C>>(p => Forgets.Unbox<R, A, B>(input).Run(p.First));

    //second扩展到Pair<C,A>取Second分量调用原Forget.run
    public new App2<Forgets.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<Forgets.Mu<R>, A, B> input)
        => Optics.Forget<R, Pair<C, A>, Pair<C, B>>(p => Forgets.Unbox<R, A, B>(input).Run(p.Second));

    //unleft从Either<A,C>取左值A调用原Forget.run
    public App2<Forgets.Mu<R>, A, B> Unleft<A, B, C>(App2<Forgets.Mu<R>, Either<A, C>, Either<B, C>> input)
        => Optics.Forget<R, A, B>(a => Forgets.Unbox<R, Either<A, C>, Either<B, C>>(input).Run(Either<A, C>.Left(a)));

    //unright从Either<C,A>取右值A调用原Forget.run
    public App2<Forgets.Mu<R>, A, B> Unright<A, B, C>(App2<Forgets.Mu<R>, Either<C, A>, Either<C, B>> input)
        => Optics.Forget<R, A, B>(a => Forgets.Unbox<R, Either<C, A>, Either<C, B>>(input).Run(Either<C, A>.Right(a)));
}
