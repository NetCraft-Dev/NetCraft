namespace NetCraft.DataFixer;

using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//乘积类型容器对应原版com.mojang.datafixers.Products
public static class Products
{
    //一元乘积P1<F,T1>持有单个App容器参数名小写避免与类型参数T1同名
    public sealed record P1<F, T1>(App<F, T1> t1) where F : K1
    {
        //扩展为二元乘积
        public P2<F, T1, T2> And<T2>(App<F, T2> t2) => new(t1, t2);
        public P3<F, T1, T2, T3> And<T2, T3>(P2<F, T2, T3> p) => new(t1, p.t1, p.t2);
        public P4<F, T1, T2, T3, T4> And<T2, T3, T4>(P3<F, T2, T3, T4> p) => new(t1, p.t1, p.t2, p.t3);
        public P5<F, T1, T2, T3, T4, T5> And<T2, T3, T4, T5>(P4<F, T2, T3, T4, T5> p) => new(t1, p.t1, p.t2, p.t3, p.t4);
        public P6<F, T1, T2, T3, T4, T5, T6> And<T2, T3, T4, T5, T6>(P5<F, T2, T3, T4, T5, T6> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5);
        public P7<F, T1, T2, T3, T4, T5, T6, T7> And<T2, T3, T4, T5, T6, T7>(P6<F, T2, T3, T4, T5, T6, T7> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5, p.t6);
        public P8<F, T1, T2, T3, T4, T5, T6, T7, T8> And<T2, T3, T4, T5, T6, T7, T8>(P7<F, T2, T3, T4, T5, T6, T7, T8> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5, p.t6, p.t7);
        public P9<F, T1, T2, T3, T4, T5, T6, T7, T8, T9> And<T2, T3, T4, T5, T6, T7, T8, T9>(P8<F, T2, T3, T4, T5, T6, T7, T8, T9> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5, p.t6, p.t7, p.t8);
        public P10<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> And<T2, T3, T4, T5, T6, T7, T8, T9, T10>(P9<F, T2, T3, T4, T5, T6, T7, T8, T9, T10> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5, p.t6, p.t7, p.t8, p.t9);
        public P11<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> And<T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(P10<F, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5, p.t6, p.t7, p.t8, p.t9, p.t10);
        public P12<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> And<T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(P11<F, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5, p.t6, p.t7, p.t8, p.t9, p.t10, p.t11);
        public P13<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> And<T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(P12<F, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5, p.t6, p.t7, p.t8, p.t9, p.t10, p.t11, p.t12);
        public P14<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> And<T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(P13<F, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5, p.t6, p.t7, p.t8, p.t9, p.t10, p.t11, p.t12, p.t13);
        public P15<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> And<T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(P14<F, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5, p.t6, p.t7, p.t8, p.t9, p.t10, p.t11, p.t12, p.t13, p.t14);
        public P16<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> And<T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(P15<F, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> p) => new(t1, p.t1, p.t2, p.t3, p.t4, p.t5, p.t6, p.t7, p.t8, p.t9, p.t10, p.t11, p.t12, p.t13, p.t14, p.t15);

        //应用一元函数到容器值
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Func<T1, R> function) where TMu : IApplicativeMu
            => Apply<TMu, R>(instance, instance.Point(function));
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, App<F, Func<T1, R>> function) where TMu : IApplicativeMu
            => instance.Ap(function, t1);
    }

    public sealed record P2<F, T1, T2>(App<F, T1> t1, App<F, T2> t2) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Func<T1, T2, R> function) where TMu : IApplicativeMu
            => Apply<TMu, R>(instance, instance.Point(function));
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, App<F, Func<T1, T2, R>> function) where TMu : IApplicativeMu
            => instance.Ap2(function, t1, t2);
    }

    public sealed record P3<F, T1, T2, T3>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function3<T1, T2, T3, R> function) where TMu : IApplicativeMu
            => Apply<TMu, R>(instance, instance.Point(function));
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, App<F, Function3<T1, T2, T3, R>> function) where TMu : IApplicativeMu
            => instance.Ap3(function, t1, t2, t3);
    }

    public sealed record P4<F, T1, T2, T3, T4>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function4<T1, T2, T3, T4, R> function) where TMu : IApplicativeMu
            => Apply<TMu, R>(instance, instance.Point(function));
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, App<F, Function4<T1, T2, T3, T4, R>> function) where TMu : IApplicativeMu
            => instance.Ap4(function, t1, t2, t3, t4);
    }

    public sealed record P5<F, T1, T2, T3, T4, T5>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function5<T1, T2, T3, T4, T5, R> function) where TMu : IApplicativeMu
            => instance.Ap5(instance.Point(function), t1, t2, t3, t4, t5);
    }

    public sealed record P6<F, T1, T2, T3, T4, T5, T6>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function6<T1, T2, T3, T4, T5, T6, R> function) where TMu : IApplicativeMu
            => instance.Ap6(instance.Point(function), t1, t2, t3, t4, t5, t6);
    }

    public sealed record P7<F, T1, T2, T3, T4, T5, T6, T7>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6, App<F, T7> t7) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function7<T1, T2, T3, T4, T5, T6, T7, R> function) where TMu : IApplicativeMu
            => instance.Ap7(instance.Point(function), t1, t2, t3, t4, t5, t6, t7);
    }

    public sealed record P8<F, T1, T2, T3, T4, T5, T6, T7, T8>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6, App<F, T7> t7, App<F, T8> t8) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function8<T1, T2, T3, T4, T5, T6, T7, T8, R> function) where TMu : IApplicativeMu
            => instance.Ap8(instance.Point(function), t1, t2, t3, t4, t5, t6, t7, t8);
    }

    public sealed record P9<F, T1, T2, T3, T4, T5, T6, T7, T8, T9>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6, App<F, T7> t7, App<F, T8> t8, App<F, T9> t9) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function9<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> function) where TMu : IApplicativeMu
            => instance.Ap9(instance.Point(function), t1, t2, t3, t4, t5, t6, t7, t8, t9);
    }

    public sealed record P10<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6, App<F, T7> t7, App<F, T8> t8, App<F, T9> t9, App<F, T10> t10) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function10<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, R> function) where TMu : IApplicativeMu
            => instance.Ap10(instance.Point(function), t1, t2, t3, t4, t5, t6, t7, t8, t9, t10);
    }

    public sealed record P11<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6, App<F, T7> t7, App<F, T8> t8, App<F, T9> t9, App<F, T10> t10, App<F, T11> t11) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function11<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, R> function) where TMu : IApplicativeMu
            => instance.Ap11(instance.Point(function), t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11);
    }

    public sealed record P12<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6, App<F, T7> t7, App<F, T8> t8, App<F, T9> t9, App<F, T10> t10, App<F, T11> t11, App<F, T12> t12) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function12<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, R> function) where TMu : IApplicativeMu
            => instance.Ap12(instance.Point(function), t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12);
    }

    public sealed record P13<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6, App<F, T7> t7, App<F, T8> t8, App<F, T9> t9, App<F, T10> t10, App<F, T11> t11, App<F, T12> t12, App<F, T13> t13) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function13<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, R> function) where TMu : IApplicativeMu
            => instance.Ap13(instance.Point(function), t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13);
    }

    public sealed record P14<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6, App<F, T7> t7, App<F, T8> t8, App<F, T9> t9, App<F, T10> t10, App<F, T11> t11, App<F, T12> t12, App<F, T13> t13, App<F, T14> t14) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function14<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, R> function) where TMu : IApplicativeMu
            => instance.Ap14(instance.Point(function), t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14);
    }

    public sealed record P15<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6, App<F, T7> t7, App<F, T8> t8, App<F, T9> t9, App<F, T10> t10, App<F, T11> t11, App<F, T12> t12, App<F, T13> t13, App<F, T14> t14, App<F, T15> t15) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function15<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, R> function) where TMu : IApplicativeMu
            => instance.Ap15(instance.Point(function), t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15);
    }

    public sealed record P16<F, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(App<F, T1> t1, App<F, T2> t2, App<F, T3> t3, App<F, T4> t4, App<F, T5> t5, App<F, T6> t6, App<F, T7> t7, App<F, T8> t8, App<F, T9> t9, App<F, T10> t10, App<F, T11> t11, App<F, T12> t12, App<F, T13> t13, App<F, T14> t14, App<F, T15> t15, App<F, T16> t16) where F : K1
    {
        public App<F, R> Apply<TMu, R>(Applicative<F, TMu> instance, Function16<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, R> function) where TMu : IApplicativeMu
            => instance.Ap16(instance.Point(function), t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15, t16);
    }

    //of工厂用IdF包装两值构造二元乘积
    public static P2<IdFs.Mu, T1, T2> Of<T1, T2>(T1 t1, T2 t2) => new(IdFs.Create(t1), IdFs.Create(t2));
}
