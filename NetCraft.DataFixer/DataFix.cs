namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Schemas;
using NetCraft.Logging;
using NetCraft.Util;
using T = NetCraft.DataFixer.Types;

//DataFix data fix base class maps to vanilla com.mojang.datafixers.DataFix
//subclasses implement makeRule to define the concrete fix logic
public abstract class DataFix
{
    private readonly Schema _outputSchema;
    private readonly bool _changesType;
    private TypeRewriteRule? _rule;

    protected DataFix(Schema outputSchema, bool changesType)
    {
        _outputSchema = outputSchema;
        _changesType = changesType;
    }

    //fixTypeEverywhere builds an all-types rewrite rule from name and type
    protected TypeRewriteRule FixTypeEverywhere<A>(string name, T.Type<A> type, Func<DynamicOps<object>, Func<A, A>> function)
        => FixTypeEverywhere(type, Unchecked<A, A>(name, type, type, function, new BitSet()));

    //convertUnchecked builds an unchecked conversion rule from name, old type, and new type
    protected TypeRewriteRule ConvertUnchecked<A, B>(string name, T.Type<A> type, T.Type<B> newType)
        => FixTypeEverywhere(type, Unchecked<A, B>(name, type, newType, _ => a => (B)(object)a!, new BitSet()));

    //writeAndRead converts the type by writing then reading by name
    protected TypeRewriteRule WriteAndRead(string name, T.Type<object> type, T.Type<object> newType)
        => WriteFixAndRead<object, object>(name, type, newType, d => d);

    //writeFixAndRead converts the type by writing, fixing, then reading by name
    protected TypeRewriteRule WriteFixAndRead<A, B>(string name, T.Type<A> type, T.Type<B> newType, Func<Dynamic<object>, Dynamic<object>> fix)
    {
        T.Type<A>? patchedType = null;
        var view = Unchecked<A, B>(name, type, newType, ops => input =>
        {
            var written = patchedType!.WriteDynamic(ops, input).ResultOrPartial(s => Log.Error(s));
            if (!written.IsPresent)
            {
                throw new InvalidOperationException("Could not write the object in " + name);
            }
            var fixedDynamic = fix(written.Get());
            var read = newType.ReadTyped(fixedDynamic).ResultOrPartial(s => Log.Error(s));
            if (!read.IsPresent)
            {
                throw new InvalidOperationException("Could not read the new object in " + name);
            }
            return read.Get().First.GetValue();
        }, new BitSet());
        var rule = FixTypeEverywhere(type, view);
        patchedType = (T.Type<A>)(object)type.All(rule, true, false).View().NewType()!;
        return rule;
    }

    //fixTypeEverywhere builds a rule from name, type, newType, and function
    protected TypeRewriteRule FixTypeEverywhere<A, B>(string name, T.Type<A> type, T.Type<B> newType, Func<DynamicOps<object>, Func<A, B>> function)
        => FixTypeEverywhere(type, Unchecked<A, B>(name, type, newType, function, new BitSet()));

    //fixTypeEverywhereTyped builds a rule from a Typed function
    protected TypeRewriteRule FixTypeEverywhereTyped<A>(string name, T.Type<A> type, Func<Typed<object>, Typed<object>> function)
        => FixTypeEverywhere(type, Checked<A, A>(name, type, type, function, new BitSet()));

    //fixTypeEverywhereTyped builds a rule from name, oldType, newType, and a Typed function
    protected TypeRewriteRule FixTypeEverywhereTyped<A, B>(string name, T.Type<A> type, T.Type<B> newType, Func<Typed<object>, Typed<object>> function)
        => FixTypeEverywhere(type, Checked<A, B>(name, type, newType, function, new BitSet()));

    //fixTypeEverywhere builds an all-types rule from type and view
    protected TypeRewriteRule FixTypeEverywhere<A, B>(T.Type<A> type, RewriteResult<A, B> view)
        => TypeRewriteRule.CheckOnce(
            TypeRewriteRule.Everywhere(
                TypeRewriteRule.IfSame(type, (RewriteResult<A, object>)(object)view),
                DataFixerUpper.OPTIMIZATION_RULE, true, true),
            t => OnFail(t));

    //unchecked wraps View.create to build a RewriteResult, maps to vanilla's private unchecked
    private static RewriteResult<A, B> Unchecked<A, B>(string name, T.Type<A> type, T.Type<B> newType, Func<DynamicOps<object>, Func<A, B>> function, BitSet bitSet)
        => RewriteResult<A, B>.Create(View<A, B>.Create(name, type, newType, function), bitSet);

    //checked builds a View from a Typed function and verifies the result type, maps to vanilla checked
    private static RewriteResult<A, B> Checked<A, B>(string name, T.Type<A> type, T.Type<B> newType, Func<Typed<object>, Typed<object>> function, BitSet bitSet)
        => RewriteResult<A, B>.Create(View<A, B>.Create(name, type, newType, ops => a =>
        {
            var result = function(new Typed<object>((T.Type<object>)(object)type!, ops, a!));
            if (!newType.Equals(result.TypeValue, true, false))
            {
                throw new InvalidOperationException("Dynamic type check failed: " + newType + " not equal to " + result.TypeValue);
            }
            return (B)(object)result.GetValue()!;
        }), bitSet);

    //onFail is invoked when the rule does not match
    protected virtual void OnFail(T.Type<object> type) { }

    //getVersionKey returns the version key of the output Schema
    public int GetVersionKey() => GetOutputSchema().GetVersionKey();

    //getRule lazily builds the rule
    public TypeRewriteRule GetRule()
    {
        _rule ??= MakeRule();
        return _rule!;
    }

    //makeRule is where subclasses build the concrete rule
    protected abstract TypeRewriteRule MakeRule();

    //getInputSchema decides the input Schema from changesType
    protected Schema GetInputSchema()
        => _changesType ? GetOutputSchema().GetParent()! : GetOutputSchema();

    //getOutputSchema returns the output Schema
    protected Schema GetOutputSchema() => _outputSchema;
}
