namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ReForgetEs container holding the Mu marker, avoiding generic nesting
public static class ReForgetEs
{
    //binary HKT marker; R is the evaluation result type
    public sealed class Mu<R> : K2 { }

    //recover the type application as ReForgetE<R,A,B>
    public static ReForgetE<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ReForgetE<R, A, B>)(object)box!;
}

//ReForgetE reverse forgetful optic with Either, maps to vanilla com.mojang.datafixers.optics.ReForgetE
//evaluator Either<A,R>->B with branch handling, used for Prism writes
public interface ReForgetE<R, A, B> : App2<ReForgetEs.Mu<R>, A, B>
{
    //run takes Either<A,R> and returns B
    B Run(Either<A, R> r);

    //ToString identifies name for debugging
    string Name { get; }
}

//ReForgetE concrete implementation holding the delegate and name
internal sealed class ReForgetEImpl<R, A, B> : ReForgetE<R, A, B>
{
    private readonly Func<Either<A, R>, B> _function;
    private readonly string _name;
    internal ReForgetEImpl(string name, Func<Either<A, R>, B> function)
    {
        _function = function;
        _name = name;
    }
    public B Run(Either<A, R> r) => _function(r);
    public string Name => _name;
    public override string ToString() => "ReForgetE_" + _name;
}

//ReForgetEInstance as the Cocartesian instance
//uses the reForgetE factory to build a new ReForgetE wrapping the dimap/left/right combination
public sealed class ReForgetEInstance<R> : Cocartesian<ReForgetEs.Mu<R>, ReForgetEInstance<R>.Mu>, App<ReForgetEInstance<R>.Mu, ReForgetEs.Mu<R>>
{
    public sealed class Mu : ICocartesianMu { }
    public static readonly ReForgetEInstance<R> InstanceOf = new();
    private ReForgetEInstance() { }

    //dimap preprocesses the input with g and postprocesses the output with h, composing the original ReForgetE.run
    public Func<App2<ReForgetEs.Mu<R>, A, B>, App2<ReForgetEs.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return input => Optics.ReForgetE<R, C, D>("dimap", e =>
        {
            var either = e.MapLeft(g);
            var b = ReForgetEs.Unbox<R, A, B>(input).Run(either);
            return h(b);
        });
    }

    //left extends ReForgetE to the either left branch, handling the nested Either
    public App2<ReForgetEs.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ReForgetEs.Mu<R>, A, B> input)
    {
        var reForgetE = ReForgetEs.Unbox<R, A, B>(input);
        return Optics.ReForgetE<R, Either<A, C>, Either<B, C>>("left",
            e => e.Map(
                e2 => e2.Map(
                    a => Either<B, C>.Left(reForgetE.Run(Either<A, R>.Left(a))),
                    Either<B, C>.Right
                ),
                r => Either<B, C>.Left(reForgetE.Run(Either<A, R>.Right(r)))
            )
        );
    }

    //right extends ReForgetE to the either right branch, handling the nested Either
    public new App2<ReForgetEs.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ReForgetEs.Mu<R>, A, B> input)
    {
        var reForgetE = ReForgetEs.Unbox<R, A, B>(input);
        return Optics.ReForgetE<R, Either<C, A>, Either<C, B>>("right",
            e => e.Map(
                e2 => e2.Map(
                    Either<C, B>.Left,
                    a => Either<C, B>.Right(reForgetE.Run(Either<A, R>.Left(a)))
                ),
                r => Either<C, B>.Right(reForgetE.Run(Either<A, R>.Right(r)))
            )
        );
    }
}
