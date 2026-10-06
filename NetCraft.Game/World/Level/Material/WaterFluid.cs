using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Material;

//WaterFluid 水 对应原版 net.minecraft.world.level.material.WaterFluid
//落差 1 档 逆流找 4 层 每 5 刻流一次 源与流动水算同族 互相能顶掉
public abstract class WaterFluid : FlowingFluid
{
    public override Fluid FlowingType => Fluids.FlowingWater;

    public override Fluid SourceType => Fluids.Water;

    //IsSame 源与流动水同族 对应原版 isSame
    public override bool IsSame(Fluid other)
        => ReferenceEquals(other, Fluids.Water) || ReferenceEquals(other, Fluids.FlowingWater);

    public override int GetDropOff(ServerLevel level) => 1;

    public override int GetSlopeFindDistance(ServerLevel level) => 4;

    public override int GetTickDelay(ServerLevel level) => 5;

    public override float ExplosionResistance => 100f;

    //GetHeight 上方压着同族流体时算满格 对应原版 getHeight
    public override float GetHeight(FluidState state, ServerLevel level, BlockPos pos)
        => HasSameAbove(state, level, pos) ? 1f : state.OwnHeight;

    //GetFlow 流向只供客户端流体渲染与实体推动 本作这两处都还没接
    public override Vec3 GetFlow(ServerLevel level, BlockPos pos, FluidState state) => Vec3.Zero;

    //CanBeReplacedWith 向下流时只要下面不是水就顶得掉 对应原版 canBeReplacedWith
    public override bool CanBeReplacedWith(FluidState state, ServerLevel level, BlockPos pos, Fluid other, Direction direction)
        => direction == Direction.Down && other.Id.Path is not ("water" or "flowing_water");

    //CreateLegacyBlock 按液面档写回水方块 对应原版 createLegacyBlock
    public override BlockState CreateLegacyBlock(FluidState state)
        => Blocks.WATER.DefaultBlockState.SetValue(BlockStateProperties.Level15, GetLegacyLevel(state));

    //CanConvertToSource 相邻两格以上源时原地生成新源 原版由 water_source_conversion 游戏规则控制
    //游戏规则读取出口还没接到存档层 这里取原版默认值 true
    protected override bool CanConvertToSource(ServerLevel level) => true;

    //Source 水源 对应原版 WaterFluid.Source
    public sealed class Source : WaterFluid
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("water");

        public override bool IsSource(FluidState state) => true;

        public override int GetAmount(FluidState state) => 8;
    }

    //Flowing 流动水 对应原版 WaterFluid.Flowing
    public sealed class Flowing : WaterFluid
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("flowing_water");

        //液面高度由状态自己带 默认状态取最低档 1 对齐原版状态表的第一项
        protected override int GetDefaultAmount() => 1;

        public override bool IsSource(FluidState state) => false;

        public override int GetAmount(FluidState state) => state.Amount;
    }
}
