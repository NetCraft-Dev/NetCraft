namespace NetCraft.DataFixer.Util;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Kinds;

//Either容器存放Mu标记避免Either<L,R>类型参数上下文
public static class Eithers
{
    //一元HKT标记R为右类型
    public sealed class Mu<R> : K1 { }
}

//二元类型对应原版com.mojang.datafixers.util.Either
public sealed class Either<L, R> : App<Eithers.Mu<R>, L>
{
    private readonly bool _isLeft;
    private readonly L? _left;
    private readonly R? _right;

    private Either(bool isLeft, L? left, R? right)
    {
        _isLeft = isLeft;
        _left = left;
        _right = right;
    }

    //还原类型应用为Either<L,R>
    public static Either<L, R> Unbox(App<Eithers.Mu<R>, L> box) => (Either<L, R>)(object)box!;

    //构造左值Either
    public static Either<L, R> Left(L value) => new(true, value, default);
    //构造右值Either
    public static Either<L, R> Right(R value) => new(false, default, value);

    public bool IsLeft => _isLeft;
    public bool IsRight => !_isLeft;

    //left取左值Optional
    public Optional<L> GetLeft() => _isLeft ? Optional<L>.Of(_left!) : Optional<L>.Empty();
    //right取右值Optional
    public Optional<R> GetRight() => _isLeft ? Optional<R>.Empty() : Optional<R>.Of(_right!);

    //mapBoth左右分支分别映射
    public Either<C, D> MapBoth<C, D>(Func<L, C> f1, Func<R, D> f2)
        => _isLeft ? Either<C, D>.Left(f1(_left!)) : Either<C, D>.Right(f2(_right!));

    //map折叠左右到同一类型
    public T Map<T>(Func<L, T> l, Func<R, T> r)
        => _isLeft ? l(_left!) : r(_right!);

    //ifLeft左值时回调
    public Either<L, R> IfLeft(Action<L> consumer)
    {
        if (_isLeft) consumer(_left!);
        return this;
    }

    //ifRight右值时回调
    public Either<L, R> IfRight(Action<R> consumer)
    {
        if (!_isLeft) consumer(_right!);
        return this;
    }

    //mapLeft仅映射左分支
    public Either<T, R> MapLeft<T>(Func<L, T> l)
        => _isLeft ? Either<T, R>.Left(l(_left!)) : Either<T, R>.Right(_right!);

    //mapRight仅映射右分支
    public Either<L, T> MapRight<T>(Func<R, T> l)
        => _isLeft ? Either<L, T>.Left(_left!) : Either<L, T>.Right(l(_right!));

    //或抛出右值为异常时抛出
    public L OrThrow() => _isLeft ? _left! : throw new InvalidOperationException(_right?.ToString());

    //swap左右互换
    public Either<R, L> Swap()
        => _isLeft ? Either<R, L>.Right(_left!) : Either<R, L>.Left(_right!);

    //flatMap左值链式
    public Either<L2, R> FlatMap<L2>(Func<L, Either<L2, R>> function)
        => _isLeft ? function(_left!) : Either<L2, R>.Right(_right!);

    //unwrap左右同类型时取值
    public static U Unwrap<U>(Either<U, U> either) => either.Map(u => u, u => u);

    public override string ToString() => _isLeft ? $"Left[{_left}]" : $"Right[{_right}]";

    public override bool Equals(object? obj)
    {
        if (obj is not Either<L, R> other) return false;
        if (_isLeft != other._isLeft) return false;
        return _isLeft ? Equals(_left, other._left) : Equals(_right, other._right);
    }

    public override int GetHashCode() => _isLeft ? _left?.GetHashCode() ?? 0 : _right?.GetHashCode() ?? 0;
}

//Either作为Applicative的实例R2为左类型独立放置避免Either<L,R>类型参数上下文
public sealed class EitherInstance<R2> : Applicative<Eithers.Mu<R2>, EitherInstance<R2>.Mu>, Traversable<Eithers.Mu<R2>, EitherInstance<R2>.Mu>, CocartesianLike<Eithers.Mu<R2>, R2, EitherInstance<R2>.Mu>
{
    public sealed class Mu : IApplicativeMu, ITraversableMu, ICocartesianLikeMu { }

    public App<Eithers.Mu<R2>, R> Map<T, R>(Func<T, R> func, App<Eithers.Mu<R2>, T> ts)
        => Either<T, R2>.Unbox(ts).MapLeft(func);

    public App<Eithers.Mu<R2>, A> Point<A>(A a) => Either<A, R2>.Left(a);

    public Func<App<Eithers.Mu<R2>, A>, App<Eithers.Mu<R2>, R>> Lift1<A, R>(App<Eithers.Mu<R2>, Func<A, R>> function)
        => a => Either<Func<A, R>, R2>.Unbox(function).FlatMap(f => Either<A, R2>.Unbox(a).MapLeft(f));

    public Func<App<Eithers.Mu<R2>, A>, App<Eithers.Mu<R2>, B>, App<Eithers.Mu<R2>, R>> Lift2<A, B, R>(App<Eithers.Mu<R2>, Func<A, B, R>> function)
        => (a, b) => Either<Func<A, B, R>, R2>.Unbox(function).FlatMap(f => Either<A, R2>.Unbox(a).FlatMap(av => Either<B, R2>.Unbox(b).MapLeft(bv => f(av, bv))));

    public App<F, App<Eithers.Mu<R2>, B>> Traverse<F, TMu2, A, B>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> function, App<Eithers.Mu<R2>, A> input) where F : K1 where TMu2 : IApplicativeMu
        => Either<A, R2>.Unbox(input).Map<App<F, App<Eithers.Mu<R2>, B>>>(
            l =>
            {
                App<F, B> b = function(l);
                Func<B, App<Eithers.Mu<R2>, B>> leftFunc = v => Either<B, R2>.Left(v);
                return applicative.Ap(leftFunc, b);
            },
            r => applicative.Point<App<Eithers.Mu<R2>, B>>(Either<B, R2>.Right(r))
        )!;

    public App<Eithers.Mu<R2>, A> To<A>(App<Eithers.Mu<R2>, A> input) => input;
    public App<Eithers.Mu<R2>, A> From<A>(App<Eithers.Mu<R2>, A> input) => input;
}
