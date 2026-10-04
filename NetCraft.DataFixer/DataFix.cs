namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Schemas;
using NetCraft.Logging;
using NetCraft.Util;
using T = NetCraft.DataFixer.Types;

//DataFix数据修复基类对应原版com.mojang.datafixers.DataFix
//子类实现makeRule定义具体修复逻辑
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

    //fixTypeEverywhere按name与type构造全类型重写规则
    protected TypeRewriteRule FixTypeEverywhere<A>(string name, T.Type<A> type, Func<DynamicOps<object>, Func<A, A>> function)
        => FixTypeEverywhere(type, Unchecked<A, A>(name, type, type, function, new BitSet()));

    //convertUnchecked按name与原类型新类型构造未检查转换规则
    protected TypeRewriteRule ConvertUnchecked<A, B>(string name, T.Type<A> type, T.Type<B> newType)
        => FixTypeEverywhere(type, Unchecked<A, B>(name, type, newType, _ => a => (B)(object)a!, new BitSet()));

    //writeAndRead按name写后读转换类型
    protected TypeRewriteRule WriteAndRead(string name, T.Type<object> type, T.Type<object> newType)
        => WriteFixAndRead<object, object>(name, type, newType, d => d);

    //writeFixAndRead按name写后修正再读转换类型
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

    //fixTypeEverywhere按name与type与newType与function构造规则
    protected TypeRewriteRule FixTypeEverywhere<A, B>(string name, T.Type<A> type, T.Type<B> newType, Func<DynamicOps<object>, Func<A, B>> function)
        => FixTypeEverywhere(type, Unchecked<A, B>(name, type, newType, function, new BitSet()));

    //fixTypeEverywhereTyped按Typed函数构造规则
    protected TypeRewriteRule FixTypeEverywhereTyped<A>(string name, T.Type<A> type, Func<Typed<object>, Typed<object>> function)
        => FixTypeEverywhere(type, Checked<A, A>(name, type, type, function, new BitSet()));

    //fixTypeEverywhereTyped按name与oldType与newType与Typed函数构造规则
    protected TypeRewriteRule FixTypeEverywhereTyped<A, B>(string name, T.Type<A> type, T.Type<B> newType, Func<Typed<object>, Typed<object>> function)
        => FixTypeEverywhere(type, Checked<A, B>(name, type, newType, function, new BitSet()));

    //fixTypeEverywhere按type与view构造全类型规则
    protected TypeRewriteRule FixTypeEverywhere<A, B>(T.Type<A> type, RewriteResult<A, B> view)
        => TypeRewriteRule.CheckOnce(
            TypeRewriteRule.Everywhere(
                TypeRewriteRule.IfSame(type, (RewriteResult<A, object>)(object)view),
                DataFixerUpper.OPTIMIZATION_RULE, true, true),
            t => OnFail(t));

    //unchecked包装View.create构造RewriteResult对应原版私有unchecked
    private static RewriteResult<A, B> Unchecked<A, B>(string name, T.Type<A> type, T.Type<B> newType, Func<DynamicOps<object>, Func<A, B>> function, BitSet bitSet)
        => RewriteResult<A, B>.Create(View<A, B>.Create(name, type, newType, function), bitSet);

    //checked按Typed函数构造View并校验结果类型对应原版checked
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

    //onFail规则未匹配时回调
    protected virtual void OnFail(T.Type<object> type) { }

    //getVersionKey返回输出Schema的版本key
    public int GetVersionKey() => GetOutputSchema().GetVersionKey();

    //getRule惰性构造规则
    public TypeRewriteRule GetRule()
    {
        _rule ??= MakeRule();
        return _rule!;
    }

    //makeRule子类实现具体规则构造
    protected abstract TypeRewriteRule MakeRule();

    //getInputSchema按changesType决定输入Schema
    protected Schema GetInputSchema()
        => _changesType ? GetOutputSchema().GetParent()! : GetOutputSchema();

    //getOutputSchema返回输出Schema
    protected Schema GetOutputSchema() => _outputSchema;
}
