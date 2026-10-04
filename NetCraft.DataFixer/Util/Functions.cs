namespace NetCraft.DataFixer.Util;

//多参数函数委托对应原版Function3..Function16
//C#用delegate代替Java函数式接口

public delegate R Function3<T1, T2, T3, R>(T1 t1, T2 t2, T3 t3);
public delegate R Function4<T1, T2, T3, T4, R>(T1 t1, T2 t2, T3 t3, T4 t4);
public delegate R Function5<T1, T2, T3, T4, T5, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5);
public delegate R Function6<T1, T2, T3, T4, T5, T6, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6);
public delegate R Function7<T1, T2, T3, T4, T5, T6, T7, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7);
public delegate R Function8<T1, T2, T3, T4, T5, T6, T7, T8, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8);
public delegate R Function9<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9);
public delegate R Function10<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, T10 t10);
public delegate R Function11<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, T10 t10, T11 t11);
public delegate R Function12<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, T10 t10, T11 t11, T12 t12);
public delegate R Function13<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, T10 t10, T11 t11, T12 t12, T13 t13);
public delegate R Function14<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, T10 t10, T11 t11, T12 t12, T13 t13, T14 t14);
public delegate R Function15<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, T10 t10, T11 t11, T12 t12, T13 t13, T14 t14, T15 t15);
public delegate R Function16<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, R>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6, T7 t7, T8 t8, T9 t9, T10 t10, T11 t11, T12 t12, T13 t13, T14 t14, T15 t15, T16 t16);
