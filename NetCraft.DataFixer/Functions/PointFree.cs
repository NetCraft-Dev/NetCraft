namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//PointFree point-free function maps to vanilla com.mojang.datafixers.functions.PointFree
//represents a cacheable optimized function wrapper; eval evaluates lazily
public abstract class PointFree<T2>
{
    private volatile bool _initialized;
    private Func<DynamicOps<object>, T2>? _value;

    //evalCached evaluates lazily and caches the result; thread-safe double-checked
    public Func<DynamicOps<object>, T2> EvalCached()
    {
        if (!_initialized)
        {
            lock (this)
            {
                if (!_initialized)
                {
                    _value = Eval();
                    _initialized = true;
                }
            }
        }
        return _value!;
    }

    //type returns the output type of this PointFree
    public abstract T.Type<T2> Type();

    //eval is provided by subclasses to supply the evaluation logic
    public abstract Func<DynamicOps<object>, T2> Eval();

    //all applies the rule to every child; defaults to returning itself
    public virtual Optional<PointFree<T2>> All(PointFreeRule rule)
        => Optional<PointFree<T2>>.Of(this);

    //one applies the rule to the single child; defaults to empty
    public virtual Optional<PointFree<T2>> One(PointFreeRule rule)
        => Optional<PointFree<T2>>.Empty();

    //toString with an indent level
    public abstract string ToString(int level);

    //toString ultimately delegates to the level-taking version
    public override string ToString() => ToString(0);

    //indent produces indentation for the given level
    public static string Indent(int level) => new(' ', level);
}
