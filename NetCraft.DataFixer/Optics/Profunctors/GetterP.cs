namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//GetterP getter profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.GetterP
//aggregates Profunctor+Bicontravariant; Getter is based on this
public interface GetterP<P, TMu> : Profunctor<P, TMu>, Bicontravariant<P, TMu> where P : K2 where TMu : IGetterPMu
{
    //recover the type application as GetterP
    static GetterP<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : IGetterPMu
        => (GetterP<P2, TMu2>)(object)proofBox;

    //secondPhantom attaches a phantom second component, composing with cimap+rmap
    //the type parameter is explicitly <Unit> instead of vanilla Void, avoiding the lack of a Void type in C#
    //uses the lambda x=>x rather than _=>_ to avoid C# discard parsing ambiguity
    App2<P, C, A> SecondPhantom<A, B, C>(App2<P, C, B> input)
        => Cimap<C, Unit, C, A>(() => Rmap<C, B, Unit>(input, _ => default!), x => x, _ => default!);
}
