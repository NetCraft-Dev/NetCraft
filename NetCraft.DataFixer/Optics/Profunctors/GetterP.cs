namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//GetterP获取profunctor对应原版com.mojang.datafixers.optics.profunctors.GetterP
//聚合Profunctor+BicontravariantGetter基于此
public interface GetterP<P, TMu> : Profunctor<P, TMu>, Bicontravariant<P, TMu> where P : K2 where TMu : IGetterPMu
{
    //还原类型应用为GetterP
    static GetterP<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : IGetterPMu
        => (GetterP<P2, TMu2>)(object)proofBox;

    //secondPhantom附加phantom第二分量用cimap+rmap组合
    //类型参数显式指定<Unit>替代原版Void避免C#无Void类型问题
    //lambda用x=>x而非_=>_避免C# discard解析歧义
    App2<P, C, A> SecondPhantom<A, B, C>(App2<P, C, B> input)
        => Cimap<C, Unit, C, A>(() => Rmap<C, B, Unit>(input, _ => default!), x => x, _ => default!);
}
