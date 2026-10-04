using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//MatchingFluidsPredicate 匹配流体集合对应原版 MatchingFluidsPredicate
public class MatchingFluidsPredicate : StateTestingPredicate
{
    //EmptyFluid 空流体占位 对应原版 Fluids.EMPTY 流体注册表尚未装载内容
    public static readonly Fluid EmptyFluid = new EmptyFluidPlaceholder();

    public static readonly Codec<MatchingFluidsPredicate> Codec =
        RecordCodecBuilder.Of2<MatchingFluidsPredicate, Vec3i, HolderSet<Fluid>>(
            StateTestingCodec().ForGetter<MatchingFluidsPredicate, Vec3i>(p => p.Offset),
            new RegistryHolderSetCodec<Fluid>(BuiltInRegistries.FLUID).FieldOf("fluids")
                .ForGetter<MatchingFluidsPredicate, HolderSet<Fluid>>(p => p._fluids),
            (offset, fluids) => new MatchingFluidsPredicate(offset, fluids));

    private readonly HolderSet<Fluid> _fluids;

    public MatchingFluidsPredicate(Vec3i offset, HolderSet<Fluid> fluids) : base(offset) => _fluids = fluids;

    public HolderSet<Fluid> Fluids => _fluids;

    //NetCraft 流体状态只区分空与非空 按流体 id 是否为 empty 近似判定
    protected override bool Test(BlockState state)
    {
        if (!_fluids.IsBound) return false;
        var fluidState = state.FluidState;
        foreach (var holder in _fluids)
        {
            var expectsEmpty = holder.Value.Id.Path == "empty";
            if (expectsEmpty == fluidState.IsEmpty) return true;
        }
        return false;
    }

    public override BlockPredicateType Type => BlockPredicateType.MatchingFluids;
}

//EmptyFluidPlaceholder 空流体占位实现 待流体注册表装载后由真实流体取代
internal sealed class EmptyFluidPlaceholder : Fluid
{
    public override Identifier Id => Identifier.WithDefaultNamespace("empty");
}
