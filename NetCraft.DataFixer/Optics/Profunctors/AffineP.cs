namespace NetCraft.DataFixer.Optics.Profunctors;

using NetCraft.DataFixer.Kinds;

//AffineP affine profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.AffineP
//aggregates Cartesian+Cocartesian; Affine is based on this
public interface AffineP<P, TMu> : Cartesian<P, TMu>, Cocartesian<P, TMu> where P : K2 where TMu : IAffinePMu
{
}
