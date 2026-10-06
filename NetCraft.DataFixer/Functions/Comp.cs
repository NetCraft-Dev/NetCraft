namespace NetCraft.DataFixer.Functions;

using System;
using System.Collections.Generic;
using System.Linq;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//Comp function composition maps to vanilla com.mojang.datafixers.functions.Comp
//composes multiple PointFree<A->B> in order into a single PointFree<A->B>
//_functions uses object[] to align with the type-erasure semantics of vanilla PointFree<? extends Function<?, ?>>[]
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

    //all applies the rule to every function; expands nested Comp while composing
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

    //one replaces the first matching function; if the result is a Comp, expand and splice it in
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

    //eval applies each function in reverse order, transforming input step by step
    public override Func<DynamicOps<object>, Func<A, B>> Eval()
        => ops => input =>
        {
            object value = input!;
            for (int i = _functions.Length - 1; i >= 0; i--)
            {
                //_functions[i] may be a PointFree subclass such as Apply, so casting to PointFree<Func<object,object>> fails
                //use Unsafe.As to bypass the runtime type check and align with Java type erasure
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
