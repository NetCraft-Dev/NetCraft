namespace NetCraft.DataFixer;

using System;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Types;
using NetCraft.Util;

//RewriteResult rewrite result maps to vanilla com.mojang.datafixers.RewriteResult
//contains view and recData recursion data; recData is a BitSet recording which recursion point indexes were hit
public sealed class RewriteResult<A, B>
{
    //view is the rewritten conversion view
    public View<A, B> ViewValue { get; }

    //recData recursion state data; a BitSet recording which recursion point indexes were touched
    public BitSet RecDataValue { get; }

    public RewriteResult(View<A, B> view, BitSet recData)
    {
        ViewValue = view;
        RecDataValue = recData;
    }

    //create factory method
    public static RewriteResult<A, B> Create(View<A, B> view, BitSet recData)
        => new(view, recData);

    //nop builds a no-op result using Id as function to guarantee NewType=type, aligning with vanilla RewriteResult.nop
    //during Cap1 chaining the next rule needs nop.NewType as input, so null cannot be used
    public static RewriteResult<A, B> Nop(Type<A> type)
        => Create(View<A, B>.NopView(type), EmptyRecData());

    //view returns the view
    public View<A, B> View() => ViewValue;

    //recData returns the recursion data
    public BitSet RecData() => RecDataValue;

    //compose connects that's output to this's input, returning a C->B result and preserving this's recData
    public RewriteResult<C, B> Compose<C>(RewriteResult<C, A> that)
        => RewriteResult<C, B>.Create(ViewValue.Compose(that.ViewValue), RecDataValue);

    //EmptyRecData empty recursion data placeholder
    private static BitSet EmptyRecData() => new();
}
