namespace NetCraft.DataFixer.Functions;

using System;
using System.Collections.Generic;
using System.Linq;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//Comp函数复合对应原版com.mojang.datafixers.functions.Comp
//多个PointFree<A->B>顺序复合为一个PointFree<A->B>
//_functions用object[]对齐原版PointFree<? extends Function<?, ?>>[]类型擦除语义
public sealed class Comp<A, B> : PointFree<Func<A, B>>
{
    private readonly object[] _functions;
    private readonly T.Type<Func<A, B>> _type;

    public Comp(object[] functions, T.Type<Func<A, B>>? type = null)
    {
        _functions = functions;
        _type = type!;
    }

    public object[] Functions() => _functions;

    public override T.Type<Func<A, B>> Type() => _type;

    //all对所有function应用规则复合时展开嵌套Comp
    public override Optional<PointFree<Func<A, B>>> All(PointFreeRule rule)
    {
        var newFunctions = new List<object>(_functions.Length);
        var rewritten = false;
        foreach (var function in _functions)
        {
            var rewrite = rule.RewriteOrNop((PointFree<Func<object, object>>)(object)function!);
            if (!ReferenceEquals(rewrite, function))
            {
                rewritten = true;
                if (rewrite is Comp<object, object> comp)
                {
                    newFunctions.AddRange(comp._functions);
                }
                else
                {
                    newFunctions.Add(rewrite);
                }
            }
            else
            {
                newFunctions.Add(function);
            }
        }
        if (rewritten)
        {
            return Optional<PointFree<Func<A, B>>>.Of(new Comp<A, B>(newFunctions.ToArray(), _type));
        }
        return Optional<PointFree<Func<A, B>>>.Of(this);
    }

    //one对首个命中function替换若结果是Comp展开插入
    public override Optional<PointFree<Func<A, B>>> One(PointFreeRule rule)
    {
        for (int i = 0; i < _functions.Length; i++)
        {
            var function = _functions[i];
            var rewrite = rule.Rewrite((PointFree<Func<object, object>>)(object)function!);
            if (rewrite.IsPresent)
            {
                var get = rewrite.Get();
                var getFunctionsLen = get is Comp<object, object> c ? c._functions.Length : 1;
                var newFunctions = new object[_functions.Length - 1 + getFunctionsLen];
                for (int j = 0; j < i; j++)
                {
                    newFunctions[j] = _functions[j];
                }
                if (get is Comp<object, object> comp)
                {
                    for (int k = 0; k < comp._functions.Length; k++)
                    {
                        newFunctions[i + k] = comp._functions[k];
                    }
                }
                else
                {
                    newFunctions[i] = get;
                }
                for (int j = i + 1; j < _functions.Length; j++)
                {
                    newFunctions[j - 1 + getFunctionsLen] = _functions[j];
                }
                return Optional<PointFree<Func<A, B>>>.Of(new Comp<A, B>(newFunctions, _type));
            }
        }
        return Optional<PointFree<Func<A, B>>>.Empty();
    }

    //eval按逆序应用每个function把input逐次变换
    public override Func<DynamicOps<object>, Func<A, B>> Eval()
        => ops => input =>
        {
            object value = input!;
            for (int i = _functions.Length - 1; i >= 0; i--)
            {
                //_functions[i]可能是Apply等PointFree子类强转PointFree<Func<object,object>>失败
                //用Unsafe.As绕过运行时类型检查对齐Java类型擦除
                var fObj = (object)_functions[i];
                var f = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref fObj);
                value = f.EvalCached()(ops)(value);
            }
            return (B)value!;
        };

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not Comp<A, B> other) return false;
        if (_functions.Length != other._functions.Length) return false;
        for (int i = 0; i < _functions.Length; i++)
        {
            if (!Equals(_functions[i], other._functions[i])) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var f in _functions)
        {
            hash.Add(f);
        }
        return hash.ToHashCode();
    }

    public override string ToString(int level)
    {
        var content = string.Join("\n" + Indent(level + 1) + "\u25E6\n" + Indent(level + 1),
            _functions.Select(f => ((PointFree<Func<object, object>>)f!).ToString(level + 1)));
        return "(\n" + Indent(level + 1) + content + "\n" + Indent(level) + ")";
    }
}
