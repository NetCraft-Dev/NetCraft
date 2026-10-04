namespace NetCraft.DataFixer;

using System;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Types;
using NetCraft.Util;

//RewriteResult重写结果对应原版com.mojang.datafixers.RewriteResult
//包含view和recData递归数据recData为BitSet记录递归点index命中
public sealed class RewriteResult<A, B>
{
    //view重写后的转换视图
    public View<A, B> ViewValue { get; }

    //recData递归状态数据BitSet记录哪些递归点index被触碰
    public BitSet RecDataValue { get; }

    public RewriteResult(View<A, B> view, BitSet recData)
    {
        ViewValue = view;
        RecDataValue = recData;
    }

    //create工厂方法
    public static RewriteResult<A, B> Create(View<A, B> view, BitSet recData)
        => new(view, recData);

    //nop构造无操作结果用Id作为function保证NewType=type对齐原版RewriteResult.nop
    //Cap1链式时下一条rule需要nop.NewType作为输入不能用null
    public static RewriteResult<A, B> Nop(Type<A> type)
        => Create(View<A, B>.NopView(type), EmptyRecData());

    //view返回视图
    public View<A, B> View() => ViewValue;

    //recData返回递归数据
    public BitSet RecData() => RecDataValue;

    //compose把that的输出接到this的输入返回C->B的结果保留this的recData
    public RewriteResult<C, B> Compose<C>(RewriteResult<C, A> that)
        => RewriteResult<C, B>.Create(ViewValue.Compose(that.ViewValue), RecDataValue);

    //EmptyRecData空递归数据占位
    private static BitSet EmptyRecData() => new();
}
