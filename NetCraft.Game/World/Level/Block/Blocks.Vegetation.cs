using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using DoubleBlockHalf = NetCraft.Registry.Enums.DoubleBlockHalf;

namespace NetCraft.Game.World.Level.Block;

//V-8 vegetation blocks ported one by one from vanilla with their shapes; growth and placement behavior are left to a later batch
//Properties always come from the injected embedded block table and are not repeated here; missing one shifts the state count
//The namespace segment named Block clashes with the Registry.Block type, so shape helpers must be fully qualified
public static partial class Blocks
{
    public static readonly TallGrassBlock SHORT_GRASS = new("short_grass");
    public static readonly TallGrassBlock FERN = new("fern");
    public static readonly DryVegetationBlock DEAD_BUSH = new("dead_bush");
    public static readonly BushBlock BUSH = new("bush");
    public static readonly ShortDryGrassBlock SHORT_DRY_GRASS = new("short_dry_grass");
    public static readonly TallDryGrassBlock TALL_DRY_GRASS = new("tall_dry_grass");
    public static readonly SeagrassBlock SEAGRASS = new("seagrass");
    public static readonly TallSeagrassBlock TALL_SEAGRASS = new("tall_seagrass");
    public static readonly SugarCaneBlock SUGAR_CANE = new("sugar_cane");
    public static readonly LilyPadBlock LILY_PAD = new("lily_pad");
    public static readonly NetherSproutsBlock NETHER_SPROUTS = new("nether_sprouts");
    public static readonly SporeBlossomBlock SPORE_BLOSSOM = new("spore_blossom");
    public static readonly AzaleaBlock AZALEA = new("azalea");
    public static readonly AzaleaBlock FLOWERING_AZALEA = new("flowering_azalea");
    public static readonly SmallDripleafBlock SMALL_DRIPLEAF = new("small_dripleaf");
    public static readonly HangingRootsBlock HANGING_ROOTS = new("hanging_roots");
    public static readonly CaveVinesBlock CAVE_VINES = new("cave_vines");
    public static readonly CaveVinesBlock CAVE_VINES_PLANT = new("cave_vines_plant");
    public static readonly KelpBlock KELP = new("kelp");
    public static readonly HangingMossBlock PALE_HANGING_MOSS = new("pale_hanging_moss");
    public static readonly SweetBerryBushBlock SWEET_BERRY_BUSH = new("sweet_berry_bush");

    //RegisterVegetation registers vegetation blocks into the real block table with the registry name as the key
    private static void RegisterVegetation(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks =
        {
            SHORT_GRASS, FERN, DEAD_BUSH, BUSH, SHORT_DRY_GRASS, TALL_DRY_GRASS,
            SEAGRASS, TALL_SEAGRASS, SUGAR_CANE, LILY_PAD, NETHER_SPROUTS, SPORE_BLOSSOM,
            AZALEA, FLOWERING_AZALEA, SMALL_DRIPLEAF, HANGING_ROOTS,
            CAVE_VINES, CAVE_VINES_PLANT, KELP, PALE_HANGING_MOSS, SWEET_BERRY_BUSH,
        };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //NamedBlock block base class carrying only a registry name, used to distinguish multiple registry names sharing a shape
    //This is common for vegetation and decoration; sixteen carpet colors and the coral colors are all different registry names of the same class
    public abstract class NamedBlock : BlockBehaviour
    {
        private readonly string _name;

        protected NamedBlock(string name) => _name = name;

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);
    }

    //VegetationBlock common base class for vegetation and crops, maps to vanilla VegetationBlock
    //By default the cell below must belong to supports_vegetation and it removes itself without support
    //Dry types switch to supports_dry_vegetation and crops to supports_crops; the rest override SupportTag with their own supports_*
    //26.2 moved "what it can be planted on" from code branches to tags, and this follows the same scheme
    public abstract class VegetationBlock : NamedBlock
    {
        protected VegetationBlock(string name) : base(name) { }

        //SupportTag which tag the cell below must belong to, the plain vegetation one by default
        protected virtual NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsVegetation;

        //MayPlaceOn whether the cell below can support this block, maps to vanilla mayPlaceOn
        protected virtual bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
            => belowState.Owner is BlockBehaviour behaviour && behaviour.IsInTag(SupportTag);

        //CanSurvive only checks the cell below, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var below = pos.Offset(Direction.Down);
            return level.GetBlockState(below) is { } belowState && MayPlaceOn(level, below, belowState);
        }

        //UpdateShape removes itself when support is gone, maps to vanilla updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (!CanSurvive(level, pos, state)) return AIR.DefaultBlockState;
            return base.UpdateShape(level, pos, state, directionToNeighbour, neighbourPos, neighbourState);
        }
    }

    //TallGrassBlock short grass and fern, the same vanilla class with two registry names
    public sealed class TallGrassBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 13.0);

        public TallGrassBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //DryVegetationBlock dead bush, the support face switches to the dry tag
    public sealed class DryVegetationBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 13.0);

        public DryVegetationBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsDryVegetation;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //BushBlock bush, the shape base class of this vanilla plant family, used flat by registry name here
    public sealed class BushBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 13.0);

        public BushBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //ShortDryGrassBlock short dry grass, uses the dry tag like the dead bush
    public sealed class ShortDryGrassBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 10.0);

        public ShortDryGrassBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsDryVegetation;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //TallDryGrassBlock tall dry grass, uses the dry tag like the dead bush
    public sealed class TallDryGrassBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);

        public TallDryGrassBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsDryVegetation;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //SeagrassBlock seagrass, the support face must be sturdy and not a magma block
    public sealed class SeagrassBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 12.0);

        public SeagrassBlock(string name) : base(name) { }

        //MayPlaceOn the upward face of the cell below must be sturdy and a magma block cannot support it, maps to vanilla mayPlaceOn
        protected override bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
            => belowState.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, belowPos, belowState, Direction.Up)
                && !behaviour.IsInTag(BlockTags.CannotSupportSeagrass);

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //TallSeagrassBlock tall seagrass, two cells with the lower half submerged in full water
    public sealed class TallSeagrassBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 16.0);

        public TallSeagrassBlock(string name) : base(name) { }

        protected override bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
            => belowState.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, belowPos, belowState, Direction.Up)
                && !behaviour.IsInTag(BlockTags.CannotSupportSeagrass);

        //CanSurvive the upper half only accepts its own lower half and the lower half checks the support face and requires full water, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (state.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.upper)
            {
                var below = pos.Offset(Direction.Down);
                return level.GetBlockState(below) is { } belowState
                    && ReferenceEquals(belowState.Owner, this)
                    && belowState.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.lower;
            }
            var fluid = level.GetBlockState(pos)?.FluidState;
            return base.CanSurvive(level, pos, state) && fluid is { IsWater: true, IsFull: true };
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //SugarCaneBlock sugar cane, stacks on the same type below, otherwise it must be on sand and have water or frosted ice beside it
    public sealed class SugarCaneBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 16.0);
        private static readonly Direction[] Horizontals =
            { Direction.North, Direction.South, Direction.West, Direction.East };

        public SugarCaneBlock(string name) : base(name) { }

        //CanSurvive passes directly on the same type below; on soil the four neighbors below must have water or frosted ice, maps to vanilla canSurvive
        //The water side uses the vanilla supports_sugar_cane_adjacently fluid tag whose content is water, so it is checked as water here
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var belowPos = pos.Offset(Direction.Down);
            if (level.GetBlockState(belowPos) is not { } belowState) return false;
            if (ReferenceEquals(belowState.Owner, this)) return true;
            if (belowState.Owner is not BlockBehaviour belowBehaviour
                || !belowBehaviour.IsInTag(BlockTags.SupportsSugarCane)) return false;
            foreach (var direction in Horizontals)
            {
                var neighbourPos = belowPos.Offset(direction);
                if (level.GetBlockState(neighbourPos) is not { } neighbour) continue;
                if (neighbour.FluidState.IsWater) return true;
                if (neighbour.Owner is BlockBehaviour behaviour
                    && behaviour.IsInTag(BlockTags.SupportsSugarCaneAdjacently)) return true;
            }
            return false;
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //LilyPadBlock lily pad, a thin sheet floating at the water surface height
    public sealed class LilyPadBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 1.5);

        public LilyPadBlock(string name) : base(name) { }

        //MayPlaceOn the cell below is water or supports_lily_pad and its own cell must have no fluid, maps to vanilla mayPlaceOn
        //The vanilla fluid side uses the supports_lily_pad tag whose content is water
        protected override bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
        {
            var supported = belowState.FluidState.IsWater
                || (belowState.Owner is BlockBehaviour behaviour
                    && behaviour.IsInTag(BlockTags.SupportsLilyPad));
            if (!supported) return false;
            return level.GetBlockState(belowPos.Offset(Direction.Up))?.FluidState.IsEmpty ?? true;
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //NetherSproutsBlock nether sprouts, the support face uses its own tag
    public sealed class NetherSproutsBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 3.0);

        public NetherSproutsBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsNetherSprouts;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //SporeBlossomBlock spore blossom, hangs from the underside of a block with the shape against the top of the cell
    public sealed class SporeBlossomBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 13.0, 16.0);

        public SporeBlossomBlock(string name) : base(name) { }

        //CanSurvive the downward face of the cell above must hold by its center check and its own cell must not be submerged, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var abovePos = pos.Offset(Direction.Up);
            if (level.GetBlockState(abovePos) is not { } aboveState
                || aboveState.Owner is not BlockBehaviour behaviour
                || !behaviour.IsFaceSturdy(EmptyBlockGetter.Instance, abovePos, aboveState,
                    Direction.Down, SupportType.Center))
                return false;
            return level.GetBlockState(pos)?.FluidState.IsWater != true;
        }

        //UpdateShape re-evaluates when the cell above changes and becomes air when it cannot hold, maps to vanilla updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Up && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return base.UpdateShape(level, pos, state, directionToNeighbour, neighbourPos, neighbourState);
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //AzaleaBlock azalea and flowering azalea, a wide top and narrow bottom joined together, the support face uses the azalea tag
    public sealed class AzaleaBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = Shapes.Or(
            NetCraft.Registry.Block.Column(16.0, 8.0, 16.0),
            NetCraft.Registry.Block.Column(4.0, 0.0, 8.0));

        public AzaleaBlock(string name) : base(name) { }

        protected override NetCraft.Registry.TagKey<NetCraft.Registry.Block> SupportTag
            => BlockTags.SupportsAzalea;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //SmallDripleafBlock small dripleaf, two cells
    public sealed class SmallDripleafBlock : VegetationBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 0.0, 13.0);

        public SmallDripleafBlock(string name) : base(name) { }

        //MayPlaceOn passes when the cell below is supports_small_dripleaf, otherwise when the cell above is a water source it uses the plain vegetation support face, maps to vanilla mayPlaceOn
        protected override bool MayPlaceOn(ServerLevel level, BlockPos belowPos, BlockState belowState)
        {
            if (belowState.Owner is BlockBehaviour behaviour
                && behaviour.IsInTag(BlockTags.SupportsSmallDripleaf)) return true;
            if (level.GetBlockState(belowPos.Offset(Direction.Up))?.FluidState.IsWater != true) return false;
            return base.MayPlaceOn(level, belowPos, belowState);
        }

        //CanSurvive the upper half only accepts its own lower half and the lower half checks the support face, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (state.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.upper)
            {
                var below = pos.Offset(Direction.Down);
                return level.GetBlockState(below) is { } belowState
                    && ReferenceEquals(belowState.Owner, this)
                    && belowState.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.lower;
            }
            return base.CanSurvive(level, pos, state);
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //HangingRootsBlock hanging roots, the downward face of the cell above must be sturdy enough
    public sealed class HangingRootsBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(12.0, 10.0, 16.0);

        public HangingRootsBlock(string name) : base(name) { }

        //CanSurvive the downward face of the cell above must be sturdy enough, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var abovePos = pos.Offset(Direction.Up);
            return level.GetBlockState(abovePos) is { } aboveState
                && aboveState.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, abovePos, aboveState, Direction.Down);
        }

        //UpdateShape re-evaluates when the cell above changes and becomes air when it cannot hold, maps to vanilla updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Up && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return base.UpdateShape(level, pos, state, directionToNeighbour, neighbourPos, neighbourState);
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //CaveVinesBlock cave vines and vine body, both vanilla parts share one shape hanging downward
    public sealed class CaveVinesBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);

        public CaveVinesBlock(string name) : base(name) { }

        //CanSurvive the cell above is any segment of the same vine family or its downward face is sturdy enough, maps to vanilla GrowingPlantBlock.canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var abovePos = pos.Offset(Direction.Up);
            if (level.GetBlockState(abovePos) is not { } aboveState) return false;
            if (aboveState.Owner is CaveVinesBlock) return true;
            return aboveState.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, abovePos, aboveState, Direction.Down);
        }

        //UpdateShape re-evaluates when the cell above changes and becomes air when it cannot hold, maps to vanilla GrowingPlantBlock.updateShape
        //Vanilla schedules a tick before removing; here it removes itself immediately to stay consistent with the vegetation family
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Up && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return base.UpdateShape(level, pos, state, directionToNeighbour, neighbourPos, neighbourState);
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //KelpBlock kelp, grows from bottom to top
    public sealed class KelpBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(16.0, 0.0, 9.0);

        public KelpBlock(string name) : base(name) { }

        //CanSurvive the cell below is the same kelp family or its upward face is sturdy enough; a block that blocks kelp vetoes it, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var belowPos = pos.Offset(Direction.Down);
            if (level.GetBlockState(belowPos) is not { } belowState) return false;
            if (belowState.Owner is not BlockBehaviour behaviour) return false;
            if (behaviour.IsInTag(BlockTags.CannotSupportKelp)) return false;
            return belowState.Owner is KelpBlock
                || behaviour.IsFaceSturdy(level, belowPos, belowState, Direction.Up);
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;
    }

    //HangingMossBlock hanging moss, the tip segment sits two pixels above the bottom of the middle segment
    public sealed class HangingMossBlock : NamedBlock
    {
        private static readonly VoxelShape ShapeBase = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);
        private static readonly VoxelShape ShapeTip = NetCraft.Registry.Block.Column(14.0, 2.0, 16.0);

        public HangingMossBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Tip) ? ShapeTip : ShapeBase;
    }

    //SweetBerryBushBlock sweet berry bush, thin as a seedling, one ring taller when grown and a full block when ripe
    public sealed class SweetBerryBushBlock : VegetationBlock
    {
        private static readonly VoxelShape ShapeSapling = NetCraft.Registry.Block.Column(10.0, 0.0, 8.0);
        private static readonly VoxelShape ShapeGrowing = NetCraft.Registry.Block.Column(14.0, 0.0, 16.0);

        public SweetBerryBushBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Age3) switch
            {
                0 => ShapeSapling,
                3 => Shapes.Block(),
                _ => ShapeGrowing,
            };
    }
}
