using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//SolidPredicate 固体判定对应原版 SolidPredicate
public class SolidPredicate : StateTestingPredicate
{
    public static readonly Codec<SolidPredicate> Codec = new SingleFieldMapCodec<SolidPredicate, Vec3i>(
        StateTestingCodec(), offset => new SolidPredicate(offset), p => p.Offset);

    public SolidPredicate(Vec3i offset) : base(offset) { }

    //NetCraft 还没有 material.isSolid 用光照衰减近似不透明固体
    protected override bool Test(BlockState state) => state.GetLightDampening() >= 15;

    public override BlockPredicateType Type => BlockPredicateType.Solid;
}
