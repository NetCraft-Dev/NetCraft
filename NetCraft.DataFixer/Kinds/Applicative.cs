namespace NetCraft.DataFixer.Kinds;

using NetCraft.DataFixer.Util;

//应用函子提供Point与Lift1抽象及Ap系列默认实现
public interface Applicative<TF, TMu> : Functor<TF, TMu> where TF : K1 where TMu : IApplicativeMu
{
    //标记IApplicativeMu链式继承IFunctorMu与K1
    interface Mu : IApplicativeMu { }

    static Applicative<TF2, TMu2> Unbox<TF2, TMu2>(App<TMu2, TF2> proofBox) where TF2 : K1 where TMu2 : IApplicativeMu
        => (Applicative<TF2, TMu2>)(object)proofBox;

    //注入值到容器
    App<TF, A> Point<A>(A a);

    //提升一元函数为容器函数
    Func<App<TF, A>, App<TF, R>> Lift1<A, R>(App<TF, Func<A, R>> function);

    //提升二元函数
    Func<App<TF, A>, App<TF, B>, App<TF, R>> Lift2<A, B, R>(App<TF, Func<A, B, R>> function)
        => (a, b) => Ap2(function, a, b);

    //提升三元函数
    Func<App<TF, T1>, App<TF, T2>, App<TF, T3>, App<TF, R>> Lift3<T1, T2, T3, R>(App<TF, Function3<T1, T2, T3, R>> function)
        => (ft1, ft2, ft3) => Ap3(function, ft1, ft2, ft3);

    //提升四元函数
    Func<App<TF, T1>, App<TF, T2>, App<TF, T3>, App<TF, T4>, App<TF, R>> Lift4<T1, T2, T3, T4, R>(App<TF, Function4<T1, T2, T3, T4, R>> function)
        => (ft1, ft2, ft3, ft4) => Ap4(function, ft1, ft2, ft3, ft4);

    //提升五元函数
    Func<App<TF, T1>, App<TF, T2>, App<TF, T3>, App<TF, T4>, App<TF, T5>, App<TF, R>> Lift5<T1, T2, T3, T4, T5, R>(App<TF, Function5<T1, T2, T3, T4, T5, R>> function)
        => (ft1, ft2, ft3, ft4, ft5) => Ap5(function, ft1, ft2, ft3, ft4, ft5);

    //提升六元函数
    Func<App<TF, T1>, App<TF, T2>, App<TF, T3>, App<TF, T4>, App<TF, T5>, App<TF, T6>, App<TF, R>> Lift6<T1, T2, T3, T4, T5, T6, R>(App<TF, Function6<T1, T2, T3, T4, T5, T6, R>> function)
        => (ft1, ft2, ft3, ft4, ft5, ft6) => Ap6(function, ft1, ft2, ft3, ft4, ft5, ft6);

    //提升七元函数
    Func<App<TF, T1>, App<TF, T2>, App<TF, T3>, App<TF, T4>, App<TF, T5>, App<TF, T6>, App<TF, T7>, App<TF, R>> Lift7<T1, T2, T3, T4, T5, T6, T7, R>(App<TF, Function7<T1, T2, T3, T4, T5, T6, T7, R>> function)
        => (ft1, ft2, ft3, ft4, ft5, ft6, ft7) => Ap7(function, ft1, ft2, ft3, ft4, ft5, ft6, ft7);

    //提升八元函数
    Func<App<TF, T1>, App<TF, T2>, App<TF, T3>, App<TF, T4>, App<TF, T5>, App<TF, T6>, App<TF, T7>, App<TF, T8>, App<TF, R>> Lift8<T1, T2, T3, T4, T5, T6, T7, T8, R>(App<TF, Function8<T1, T2, T3, T4, T5, T6, T7, T8, R>> function)
        => (ft1, ft2, ft3, ft4, ft5, ft6, ft7, ft8) => Ap8(function, ft1, ft2, ft3, ft4, ft5, ft6, ft7, ft8);

    //提升九元函数
    Func<App<TF, T1>, App<TF, T2>, App<TF, T3>, App<TF, T4>, App<TF, T5>, App<TF, T6>, App<TF, T7>, App<TF, T8>, App<TF, T9>, App<TF, R>> Lift9<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(App<TF, Function9<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>> function)
        => (ft1, ft2, ft3, ft4, ft5, ft6, ft7, ft8, ft9) => Ap9(function, ft1, ft2, ft3, ft4, ft5, ft6, ft7, ft8, ft9);

    //应用容器函数到容器值
    App<TF, R> Ap<A, R>(App<TF, Func<A, R>> func, App<TF, A> arg)
        => Lift1(func).Invoke(arg);

    //应用普通函数到容器值等价Map
    App<TF, R> Ap<A, R>(Func<A, R> func, App<TF, A> arg)
        => Map(func, arg);

    //二元应用中间变量拆分嵌套调用便于类型推断
    App<TF, R> Ap2<A, B, R>(App<TF, Func<A, B, R>> function, App<TF, A> a, App<TF, B> b)
    {
        Func<Func<A, B, R>, Func<A, Func<B, R>>> curry = f => x => y => f(x, y);
        App<TF, Func<A, Func<B, R>>> curried = Map(curry, function);
        App<TF, Func<B, R>> applied = Ap(curried, a);
        return Ap(applied, b);
    }

    //3元应用curry后链式Ap
    App<TF, R> Ap3<T1, T2, T3, R>(App<TF, Function3<T1, T2, T3, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3)
    {
        Func<Function3<T1, T2, T3, R>, Func<T1, Func<T2, Func<T3, R>>>> curry = f => a => b => c => f(a, b, c);
        App<TF, Func<T1, Func<T2, Func<T3, R>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, R>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, R>> r2 = Ap(r1, t2);
        return Ap(r2, t3);
    }
    //4元应用curry后链式Ap
    App<TF, R> Ap4<T1, T2, T3, T4, R>(App<TF, Function4<T1, T2, T3, T4, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4)
    {
        Func<Function4<T1, T2, T3, T4, R>, Func<T1, Func<T2, Func<T3, Func<T4, R>>>>> curry = f => a => b => c => d => f(a, b, c, d);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, R>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, R>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, R>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, R>> r3 = Ap(r2, t3);
        return Ap(r3, t4);
    }
    //5元应用curry后链式Ap
    App<TF, R> Ap5<T1, T2, T3, T4, T5, R>(App<TF, Function5<T1, T2, T3, T4, T5, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5)
    {
        Func<Function5<T1, T2, T3, T4, T5, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, R>>>>>> curry = f => a => b => c => d => e => f(a, b, c, d, e);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, R>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, R>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, R>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, R>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, R>> r4 = Ap(r3, t4);
        return Ap(r4, t5);
    }
    //6元应用curry后链式Ap
    App<TF, R> Ap6<T1, T2, T3, T4, T5, T6, R>(App<TF, Function6<T1, T2, T3, T4, T5, T6, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6)
    {
        Func<Function6<T1, T2, T3, T4, T5, T6, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, R>>>>>>> curry = f => a => b => c => d => e => g => f(a, b, c, d, e, g);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, R>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, R>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, R>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, R>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, R>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, R>> r5 = Ap(r4, t5);
        return Ap(r5, t6);
    }
    //7元应用curry后链式Ap
    App<TF, R> Ap7<T1, T2, T3, T4, T5, T6, T7, R>(App<TF, Function7<T1, T2, T3, T4, T5, T6, T7, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7)
    {
        Func<Function7<T1, T2, T3, T4, T5, T6, T7, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, R>>>>>>>> curry = f => a => b => c => d => e => g => h => f(a, b, c, d, e, g, h);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, R>>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, R>>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, R>>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, Func<T7, R>>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, Func<T7, R>>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, Func<T7, R>>> r5 = Ap(r4, t5);
        App<TF, Func<T7, R>> r6 = Ap(r5, t6);
        return Ap(r6, t7);
    }
    //8元应用curry后链式Ap
    App<TF, R> Ap8<T1, T2, T3, T4, T5, T6, T7, T8, R>(App<TF, Function8<T1, T2, T3, T4, T5, T6, T7, T8, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8)
    {
        Func<Function8<T1, T2, T3, T4, T5, T6, T7, T8, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, R>>>>>>>>> curry = f => a => b => c => d => e => g => h => i => f(a, b, c, d, e, g, h, i);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, R>>>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, R>>>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, R>>>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, R>>>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, Func<T7, Func<T8, R>>>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, Func<T7, Func<T8, R>>>> r5 = Ap(r4, t5);
        App<TF, Func<T7, Func<T8, R>>> r6 = Ap(r5, t6);
        App<TF, Func<T8, R>> r7 = Ap(r6, t7);
        return Ap(r7, t8);
    }
    //9元应用curry后链式Ap
    App<TF, R> Ap9<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(App<TF, Function9<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9)
    {
        Func<Function9<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, R>>>>>>>>>> curry = f => a => b => c => d => e => g => h => i => j => f(a, b, c, d, e, g, h, i, j);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, R>>>>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, R>>>>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, R>>>>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, R>>>>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, R>>>>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, Func<T7, Func<T8, Func<T9, R>>>>> r5 = Ap(r4, t5);
        App<TF, Func<T7, Func<T8, Func<T9, R>>>> r6 = Ap(r5, t6);
        App<TF, Func<T8, Func<T9, R>>> r7 = Ap(r6, t7);
        App<TF, Func<T9, R>> r8 = Ap(r7, t8);
        return Ap(r8, t9);
    }
    //10元应用curry后链式Ap
    App<TF, R> Ap10<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, R>(App<TF, Function10<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10)
    {
        Func<Function10<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, R>>>>>>>>>>> curry = f => a => b => c => d => e => g => h => i => j => k => f(a, b, c, d, e, g, h, i, j, k);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, R>>>>>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, R>>>>>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, R>>>>>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, R>>>>>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, R>>>>>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, R>>>>>> r5 = Ap(r4, t5);
        App<TF, Func<T7, Func<T8, Func<T9, Func<T10, R>>>>> r6 = Ap(r5, t6);
        App<TF, Func<T8, Func<T9, Func<T10, R>>>> r7 = Ap(r6, t7);
        App<TF, Func<T9, Func<T10, R>>> r8 = Ap(r7, t8);
        App<TF, Func<T10, R>> r9 = Ap(r8, t9);
        return Ap(r9, t10);
    }
    //11元应用curry后链式Ap
    App<TF, R> Ap11<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, R>(App<TF, Function11<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11)
    {
        Func<Function11<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, R>>>>>>>>>>>> curry = f => a => b => c => d => e => g => h => i => j => k => l => f(a, b, c, d, e, g, h, i, j, k, l);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, R>>>>>>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, R>>>>>>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, R>>>>>>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, R>>>>>>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, R>>>>>>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, R>>>>>>> r5 = Ap(r4, t5);
        App<TF, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, R>>>>>> r6 = Ap(r5, t6);
        App<TF, Func<T8, Func<T9, Func<T10, Func<T11, R>>>>> r7 = Ap(r6, t7);
        App<TF, Func<T9, Func<T10, Func<T11, R>>>> r8 = Ap(r7, t8);
        App<TF, Func<T10, Func<T11, R>>> r9 = Ap(r8, t9);
        App<TF, Func<T11, R>> r10 = Ap(r9, t10);
        return Ap(r10, t11);
    }
    //12元应用curry后链式Ap
    App<TF, R> Ap12<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, R>(App<TF, Function12<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11, App<TF, T12> t12)
    {
        Func<Function12<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, R>>>>>>>>>>>>> curry = f => a => b => c => d => e => g => h => i => j => k => l => m => f(a, b, c, d, e, g, h, i, j, k, l, m);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, R>>>>>>>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, R>>>>>>>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, R>>>>>>>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, R>>>>>>>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, R>>>>>>>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, R>>>>>>>> r5 = Ap(r4, t5);
        App<TF, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, R>>>>>>> r6 = Ap(r5, t6);
        App<TF, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, R>>>>>> r7 = Ap(r6, t7);
        App<TF, Func<T9, Func<T10, Func<T11, Func<T12, R>>>>> r8 = Ap(r7, t8);
        App<TF, Func<T10, Func<T11, Func<T12, R>>>> r9 = Ap(r8, t9);
        App<TF, Func<T11, Func<T12, R>>> r10 = Ap(r9, t10);
        App<TF, Func<T12, R>> r11 = Ap(r10, t11);
        return Ap(r11, t12);
    }
    //13元应用curry后链式Ap
    App<TF, R> Ap13<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, R>(App<TF, Function13<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11, App<TF, T12> t12, App<TF, T13> t13)
    {
        Func<Function13<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>>>>>>>>>>> curry = f => a => b => c => d => e => g => h => i => j => k => l => m => n => f(a, b, c, d, e, g, h, i, j, k, l, m, n);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>>>>>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>>>>>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>>>>>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>>>>>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>>>>>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>>>>>> r5 = Ap(r4, t5);
        App<TF, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>>>>> r6 = Ap(r5, t6);
        App<TF, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>>>> r7 = Ap(r6, t7);
        App<TF, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>>> r8 = Ap(r7, t8);
        App<TF, Func<T10, Func<T11, Func<T12, Func<T13, R>>>>> r9 = Ap(r8, t9);
        App<TF, Func<T11, Func<T12, Func<T13, R>>>> r10 = Ap(r9, t10);
        App<TF, Func<T12, Func<T13, R>>> r11 = Ap(r10, t11);
        App<TF, Func<T13, R>> r12 = Ap(r11, t12);
        return Ap(r12, t13);
    }
    //14元应用curry后链式Ap
    App<TF, R> Ap14<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, R>(App<TF, Function14<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11, App<TF, T12> t12, App<TF, T13> t13, App<TF, T14> t14)
    {
        Func<Function14<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>>>>>>>>>>> curry = f => a => b => c => d => e => g => h => i => j => k => l => m => n => o => f(a, b, c, d, e, g, h, i, j, k, l, m, n, o);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>>>>>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>>>>>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>>>>>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>>>>>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>>>>>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>>>>>> r5 = Ap(r4, t5);
        App<TF, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>>>>> r6 = Ap(r5, t6);
        App<TF, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>>>> r7 = Ap(r6, t7);
        App<TF, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>>> r8 = Ap(r7, t8);
        App<TF, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>>> r9 = Ap(r8, t9);
        App<TF, Func<T11, Func<T12, Func<T13, Func<T14, R>>>>> r10 = Ap(r9, t10);
        App<TF, Func<T12, Func<T13, Func<T14, R>>>> r11 = Ap(r10, t11);
        App<TF, Func<T13, Func<T14, R>>> r12 = Ap(r11, t12);
        App<TF, Func<T14, R>> r13 = Ap(r12, t13);
        return Ap(r13, t14);
    }
    //15元应用curry后链式Ap
    App<TF, R> Ap15<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, R>(App<TF, Function15<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11, App<TF, T12> t12, App<TF, T13> t13, App<TF, T14> t14, App<TF, T15> t15)
    {
        Func<Function15<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>>>>>>>>>>> curry = f => a => b => c => d => e => g => h => i => j => k => l => m => n => o => p => f(a, b, c, d, e, g, h, i, j, k, l, m, n, o, p);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>>>>>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>>>>>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>>>>>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>>>>>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>>>>>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>>>>>> r5 = Ap(r4, t5);
        App<TF, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>>>>> r6 = Ap(r5, t6);
        App<TF, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>>>> r7 = Ap(r6, t7);
        App<TF, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>>> r8 = Ap(r7, t8);
        App<TF, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>>> r9 = Ap(r8, t9);
        App<TF, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>>> r10 = Ap(r9, t10);
        App<TF, Func<T12, Func<T13, Func<T14, Func<T15, R>>>>> r11 = Ap(r10, t11);
        App<TF, Func<T13, Func<T14, Func<T15, R>>>> r12 = Ap(r11, t12);
        App<TF, Func<T14, Func<T15, R>>> r13 = Ap(r12, t13);
        App<TF, Func<T15, R>> r14 = Ap(r13, t14);
        return Ap(r14, t15);
    }
    //16元应用curry后链式Ap
    App<TF, R> Ap16<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, R>(App<TF, Function16<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, R>> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11, App<TF, T12> t12, App<TF, T13> t13, App<TF, T14> t14, App<TF, T15> t15, App<TF, T16> t16)
    {
        Func<Function16<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, R>, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>>>>>>>>>>> curry = f => a => b => c => d => e => g => h => i => j => k => l => m => n => o => p => q => f(a, b, c, d, e, g, h, i, j, k, l, m, n, o, p, q);
        App<TF, Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>>>>>>>>>>> curried = Map(curry, func);
        App<TF, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>>>>>>>>>> r1 = Ap(curried, t1);
        App<TF, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>>>>>>>>> r2 = Ap(r1, t2);
        App<TF, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>>>>>>>> r3 = Ap(r2, t3);
        App<TF, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>>>>>>> r4 = Ap(r3, t4);
        App<TF, Func<T6, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>>>>>> r5 = Ap(r4, t5);
        App<TF, Func<T7, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>>>>> r6 = Ap(r5, t6);
        App<TF, Func<T8, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>>>> r7 = Ap(r6, t7);
        App<TF, Func<T9, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>>> r8 = Ap(r7, t8);
        App<TF, Func<T10, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>>> r9 = Ap(r8, t9);
        App<TF, Func<T11, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>>> r10 = Ap(r9, t10);
        App<TF, Func<T12, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>>> r11 = Ap(r10, t11);
        App<TF, Func<T13, Func<T14, Func<T15, Func<T16, R>>>>> r12 = Ap(r11, t12);
        App<TF, Func<T14, Func<T15, Func<T16, R>>>> r13 = Ap(r12, t13);
        App<TF, Func<T15, Func<T16, R>>> r14 = Ap(r13, t14);
        App<TF, Func<T16, R>> r15 = Ap(r14, t15);
        return Ap(r15, t16);
    }

    //应用普通二元函数
    App<TF, R> Apply2<A, B, R>(Func<A, B, R> func, App<TF, A> a, App<TF, B> b)
        => Ap2(Point(func), a, b);

    //应用普通三元函数
    App<TF, R> Apply3<T1, T2, T3, R>(Function3<T1, T2, T3, R> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3)
        => Ap3(Point(func), t1, t2, t3);

    //应用普通四元函数
    App<TF, R> Apply4<T1, T2, T3, T4, R>(Function4<T1, T2, T3, T4, R> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4)
        => Ap4(Point(func), t1, t2, t3, t4);

    //应用普通五元函数
    App<TF, R> Apply5<T1, T2, T3, T4, T5, R>(Function5<T1, T2, T3, T4, T5, R> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5)
        => Ap5(Point(func), t1, t2, t3, t4, t5);

    //应用普通六元函数
    App<TF, R> Apply6<T1, T2, T3, T4, T5, T6, R>(Function6<T1, T2, T3, T4, T5, T6, R> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6)
        => Ap6(Point(func), t1, t2, t3, t4, t5, t6);

    //应用普通七元函数
    App<TF, R> Apply7<T1, T2, T3, T4, T5, T6, T7, R>(Function7<T1, T2, T3, T4, T5, T6, T7, R> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7)
        => Ap7(Point(func), t1, t2, t3, t4, t5, t6, t7);

    //应用普通八元函数
    App<TF, R> Apply8<T1, T2, T3, T4, T5, T6, T7, T8, R>(Function8<T1, T2, T3, T4, T5, T6, T7, T8, R> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8)
        => Ap8(Point(func), t1, t2, t3, t4, t5, t6, t7, t8);

    //应用普通九元函数
    App<TF, R> Apply9<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(Function9<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> func, App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9)
        => Ap9(Point(func), t1, t2, t3, t4, t5, t6, t7, t8, t9);
}