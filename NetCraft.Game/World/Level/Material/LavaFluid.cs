using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.Material;

//LavaFluid 岩浆 对应原版 net.minecraft.world.level.material.LavaFluid
//落差 2 档 逆流只找 2 层 每 30 刻流一次 往下碰到水会把那格换成石头
//原版还有快速岩浆维度属性 本作维度属性系统未接入 一律按普通岩浆
public abstract class LavaFluid : FlowingFluid
{
    public const int LightEmission = 15;

    public override Fluid FlowingType => Fluids.FlowingLava;

    public override Fluid SourceType => Fluids.Lava;

    public override bool IsSame(Fluid other)
        => ReferenceEquals(other, Fluids.Lava) || ReferenceEquals(other, Fluids.FlowingLava);

    public override int GetDropOff(ServerLevel level) => 2;

    public override int GetSlopeFindDistance(ServerLevel level) => 2;

    public override int GetTickDelay(ServerLevel level) => 30;

    public override float ExplosionResistance => 100f;

    public override bool IsRandomlyTicking => true;

    //RandomTick 原版在这里让岩浆点燃四周可燃物 依赖火焰方块与可燃判定 这两个子系统未接 先留空
    public override void RandomTick(ServerLevel level, BlockPos pos, FluidState fluidState, RandomSource random) { }

    public override float GetHeight(FluidState state, ServerLevel level, BlockPos pos)
        => HasSameAbove(state, level, pos) ? 1f : state.OwnHeight;

    public override Vec3 GetFlow(ServerLevel level, BlockPos pos, FluidState state) => Vec3.Zero;

    //CanBeReplacedWith 液面够高且流过来的是水时让位 对应原版 canBeReplacedWith
    public override bool CanBeReplacedWith(FluidState state, ServerLevel level, BlockPos pos, Fluid other, Direction direction)
        => state.OwnHeight >= 0.44444445f && other.Id.Path is "water" or "flowing_water";

    public override BlockState CreateLegacyBlock(FluidState state)
        => Blocks.LAVA.DefaultBlockState.SetValue(BlockStateProperties.Level15, GetLegacyLevel(state));

    //CanConvertToSource 原版由 lava_source_conversion 游戏规则控制 默认关闭 这里取默认值
    protected override bool CanConvertToSource(ServerLevel level) => false;

    //SpreadTo 往下流时如果落点格是水 水会消失并凝结成石头 对应原版 LavaFluid.spreadTo
    protected override void SpreadTo(ServerLevel level, BlockPos pos, BlockState state, Direction direction, FluidState target)
    {
        if (direction == Direction.Down && level.GetFluidState(pos).IsWater)
        {
            //落点格本身要是流体方块才凝结 原版判的是 instanceof LiquidBlock 本作按该格带流体状态判
            if (!state.FluidState.IsEmpty) level.SetBlock(pos, Blocks.STONE.DefaultBlockState, 3);
            level.LevelEvent(1501, pos, 0);
            return;
        }
        base.SpreadTo(level, pos, state, direction, target);
    }

    //Source 岩浆源 对应原版 LavaFluid.Source
    public sealed class Source : LavaFluid
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("lava");

        public override bool IsSource(FluidState state) => true;

        public override int GetAmount(FluidState state) => 8;
    }

    //Flowing 流动岩浆 对应原版 LavaFluid.Flowing
    public sealed class Flowing : LavaFluid
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("flowing_lava");

        protected override int GetDefaultAmount() => 1;

        public override bool IsSource(FluidState state) => false;

        public override int GetAmount(FluidState state) => state.Amount;
    }
}
