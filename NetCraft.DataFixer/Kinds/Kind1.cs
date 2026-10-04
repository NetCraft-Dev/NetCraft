namespace NetCraft.DataFixer.Kinds;

using NetCraft.DataFixer;

//一元类型构造器接口提供Unbox还原与group乘积组合
public interface Kind1<TF, TMu> : App<TMu, TF> where TF : K1 where TMu : IKind1Mu
{
    //类型类标记继承非泛型IKind1Mu
    interface Mu : K1, IKind1Mu { }

    static Kind1<TF2, TMu2> Unbox<TF2, TMu2>(App<TMu2, TF2> proofBox) where TF2 : K1 where TMu2 : IKind1Mu
        => (Kind1<TF2, TMu2>)(object)proofBox;

    //group组合多个App为乘积默认实现委托Products
    Products.P1<TF, T1> Group<T1>(App<TF, T1> t1) => new(t1);
    Products.P2<TF, T1, T2> Group<T1, T2>(App<TF, T1> t1, App<TF, T2> t2) => new(t1, t2);
    Products.P3<TF, T1, T2, T3> Group<T1, T2, T3>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3) => new(t1, t2, t3);
    Products.P4<TF, T1, T2, T3, T4> Group<T1, T2, T3, T4>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4) => new(t1, t2, t3, t4);
    Products.P5<TF, T1, T2, T3, T4, T5> Group<T1, T2, T3, T4, T5>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5) => new(t1, t2, t3, t4, t5);
    Products.P6<TF, T1, T2, T3, T4, T5, T6> Group<T1, T2, T3, T4, T5, T6>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6) => new(t1, t2, t3, t4, t5, t6);
    Products.P7<TF, T1, T2, T3, T4, T5, T6, T7> Group<T1, T2, T3, T4, T5, T6, T7>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7) => new(t1, t2, t3, t4, t5, t6, t7);
    Products.P8<TF, T1, T2, T3, T4, T5, T6, T7, T8> Group<T1, T2, T3, T4, T5, T6, T7, T8>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8) => new(t1, t2, t3, t4, t5, t6, t7, t8);
    Products.P9<TF, T1, T2, T3, T4, T5, T6, T7, T8, T9> Group<T1, T2, T3, T4, T5, T6, T7, T8, T9>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9) => new(t1, t2, t3, t4, t5, t6, t7, t8, t9);
    Products.P10<TF, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> Group<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10) => new(t1, t2, t3, t4, t5, t6, t7, t8, t9, t10);
    Products.P11<TF, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> Group<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11) => new(t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11);
    Products.P12<TF, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> Group<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11, App<TF, T12> t12) => new(t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12);
    Products.P13<TF, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> Group<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11, App<TF, T12> t12, App<TF, T13> t13) => new(t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13);
    Products.P14<TF, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> Group<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11, App<TF, T12> t12, App<TF, T13> t13, App<TF, T14> t14) => new(t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14);
    Products.P15<TF, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> Group<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11, App<TF, T12> t12, App<TF, T13> t13, App<TF, T14> t14, App<TF, T15> t15) => new(t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15);
    Products.P16<TF, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> Group<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(App<TF, T1> t1, App<TF, T2> t2, App<TF, T3> t3, App<TF, T4> t4, App<TF, T5> t5, App<TF, T6> t6, App<TF, T7> t7, App<TF, T8> t8, App<TF, T9> t9, App<TF, T10> t10, App<TF, T11> t11, App<TF, T12> t12, App<TF, T13> t13, App<TF, T14> t14, App<TF, T15> t15, App<TF, T16> t16) => new(t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15, t16);
}
