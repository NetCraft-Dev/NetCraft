namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;

//AffineP仿射profunctor对应原版com.mojang.datafixers.optics.profunctors.AffineP
//聚合Cartesian+CocartesianAffine基于此
public interface AffineP<P, TMu> : Cartesian<P, TMu>, Cocartesian<P, TMu> where P : K2 where TMu : IAffinePMu
{
}
