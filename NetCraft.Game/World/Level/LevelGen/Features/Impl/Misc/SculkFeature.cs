using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//SculkPatchConfiguration sculk patch configuration, maps to vanilla SculkPatchConfiguration
public sealed class SculkPatchConfiguration : FeatureConfiguration
{
    public static readonly Codec<SculkPatchConfiguration> Codec =
        RecordCodecBuilder.Of7<SculkPatchConfiguration, int, int, int, int, int, IntProvider, float>(
            Codecs.Int.FieldOf("charge_count").ForGetter<SculkPatchConfiguration, int>(c => c.ChargeCount),
            Codecs.Int.FieldOf("amount_per_charge").ForGetter<SculkPatchConfiguration, int>(c => c.AmountPerCharge),
            Codecs.Int.FieldOf("spread_attempts").ForGetter<SculkPatchConfiguration, int>(c => c.SpreadAttempts),
            Codecs.Int.FieldOf("growth_rounds").ForGetter<SculkPatchConfiguration, int>(c => c.GrowthRounds),
            Codecs.Int.FieldOf("spread_rounds").ForGetter<SculkPatchConfiguration, int>(c => c.SpreadRounds),
            IntProviders.Codec.FieldOf("extra_rare_growths")
                .ForGetter<SculkPatchConfiguration, IntProvider>(c => c.ExtraRareGrowths),
            Codecs.Float.FieldOf("catalyst_chance")
                .ForGetter<SculkPatchConfiguration, float>(c => c.CatalystChance),
            (chargeCount, amountPerCharge, spreadAttempts, growthRounds, spreadRounds, extraRareGrowths,
                catalystChance) => new SculkPatchConfiguration(chargeCount, amountPerCharge, spreadAttempts,
                growthRounds, spreadRounds, extraRareGrowths, catalystChance));

    public int ChargeCount { get; }
    public int AmountPerCharge { get; }
    public int SpreadAttempts { get; }
    public int GrowthRounds { get; }
    public int SpreadRounds { get; }
    public IntProvider ExtraRareGrowths { get; }
    public float CatalystChance { get; }

    public SculkPatchConfiguration(int chargeCount, int amountPerCharge, int spreadAttempts, int growthRounds,
        int spreadRounds, IntProvider extraRareGrowths, float catalystChance)
    {
        ChargeCount = chargeCount;
        AmountPerCharge = amountPerCharge;
        SpreadAttempts = spreadAttempts;
        GrowthRounds = growthRounds;
        SpreadRounds = spreadRounds;
        ExtraRareGrowths = extraRareGrowths;
        CatalystChance = catalystChance;
    }
}

//SculkPatchFeature sculk patch feature, maps to vanilla SculkPatchFeature
//Scatters charge-carrying cursors from the origin and each round has them turn nearby blocks into sculk veins or sculk blocks
//Spread rounds and attempt counts set how much random is consumed; a mismatch yields different patches for the same seed
public sealed class SculkPatchFeature : Feature<SculkPatchConfiguration>
{
    private const string FeatureId = "sculk_patch";

    public static readonly SculkPatchFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new SculkPatchFeature());

    private SculkPatchFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), SculkPatchConfiguration.Codec) { }

    protected override bool Place(SculkPatchConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        if (!CanSpreadFrom(level, origin)) return false;
        var random = context.Random;
        var spreader = SculkSpreader.CreateWorldGenSpreader();
        var totalRounds = config.SpreadRounds + config.GrowthRounds;
        for (var round = 0; round < totalRounds; round++)
        {
            for (var i = 0; i < config.ChargeCount; i++) spreader.AddCursors(origin, config.AmountPerCharge);
            var spreadVeins = round < config.SpreadRounds;
            for (var i = 0; i < config.SpreadAttempts; i++) spreader.UpdateCursors(level, origin, random, spreadVeins);
            spreader.Clear();
        }
        var below = origin.Offset(0, -1, 0);
        var belowState = level.GetBlockState(below.X, below.Y, below.Z);
        if (random.NextFloat() <= config.CatalystChance && IsCollisionShapeFullBlock(belowState, below))
            level.SetBlockState(below.X, below.Y, below.Z, VegetationSupport.StateOf("sculk_catalyst"));
        var extraGrowths = config.ExtraRareGrowths.Sample(random);
        for (var i = 0; i < extraGrowths; i++)
        {
            var candidate = origin.Offset(random.NextInt(5) - 2, 0, random.NextInt(5) - 2);
            var candidateState = level.GetBlockState(candidate.X, candidate.Y, candidate.Z);
            var supportPos = candidate.Offset(0, -1, 0);
            if (!candidateState.Owner.IsAir) continue;
            if (!VegetationSupport.IsFaceSturdy(level.GetBlockState(supportPos.X, supportPos.Y, supportPos.Z),
                    Direction.Up)) continue;
            level.SetBlockState(candidate.X, candidate.Y, candidate.Z,
                VegetationSupport.WithProperty(VegetationSupport.StateOf("sculk_shrieker"), "can_summon", true));
        }
        return true;
    }

    //CanSpreadFrom whether the cell can act as a spread origin, maps to vanilla canSpreadFrom
    //It can grow only if the origin is a sculk block, or is air/water and has a full solid face in all six directions
    private static bool CanSpreadFrom(WorldGenRegion level, BlockPos origin)
    {
        var start = level.GetBlockState(origin.X, origin.Y, origin.Z);
        if (SculkBehaviourHandler.IsSculkBlock(start)) return true;
        if (start.Owner.IsAir || (start.Owner == SculkSupport.WaterBlock && !start.FluidState.IsEmpty))
        {
            foreach (var direction in Direction.Values)
            {
                var pos = origin.Offset(direction);
                if (IsCollisionShapeFullBlock(level.GetBlockState(pos.X, pos.Y, pos.Z), pos)) return true;
            }
            return false;
        }
        return false;
    }

    //IsCollisionShapeFullBlock whether the state's collision shape fills the whole cell
    private static bool IsCollisionShapeFullBlock(BlockState state, BlockPos pos)
        => state.Owner is BlockBehaviour behaviour
            && behaviour.IsCollisionShapeFullBlock(state, EmptyBlockGetter.Instance, pos);
}

//SculkSupport block checks and tags shared by the sculk subsystem, maps to the static members scattered across vanilla
internal static class SculkSupport
{
    //SculkReplaceableWorldGenTag blocks replaceable by sculk during world generation, maps to vanilla BlockTags.SCULK_REPLACEABLE_WORLD_GEN
    public static readonly TagKey<RegBlock> ReplaceableWorldGenTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("sculk_replaceable_world_gen"));

    //SculkReplaceableTag blocks replaceable by sculk, maps to vanilla BlockTags.SCULK_REPLACEABLE
    public static readonly TagKey<RegBlock> ReplaceableTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("sculk_replaceable"));

    //FireTag fire blocks; sculk veins do not grow on fire. maps to vanilla BlockTags.FIRE
    public static readonly TagKey<RegBlock> FireTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("fire"));

    //WaterBlock the water block; the fluid state system only tracks presence, and water is always a source during world generation
    public static readonly RegBlock WaterBlock = VegetationSupport.BlockOf("water");

    //GetState read the block state from the world
    public static BlockState GetState(WorldGenRegion level, BlockPos pos)
        => level.GetBlockState(pos.X, pos.Y, pos.Z);

    //SetState write the block state
    public static void SetState(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.SetBlockState(pos.X, pos.Y, pos.Z, state);

    //HasFace whether a face of the state is lit, maps to vanilla MultifaceBlock.hasFace
    public static bool HasFace(BlockState state, Direction face)
    {
        var name = VegetationSupport.FaceName(face);
        foreach (var property in state.GetProperties())
            if (property.Name == name && property is BooleanProperty boolProperty)
                return state.GetValueOrElse(boolProperty, false);
        return false;
    }

    //WithFace change the lit state of one face, maps to vanilla setValue(getFaceProperty(...))
    public static BlockState WithFace(BlockState state, Direction face, bool value)
        => VegetationSupport.WithProperty(state, VegetationSupport.FaceName(face), value);

    //HasAnyFace at least one face is lit, maps to vanilla MultifaceBlock.hasAnyFace
    public static bool HasAnyFace(BlockState state)
    {
        foreach (var direction in Direction.Values)
            if (HasFace(state, direction)) return true;
        return false;
    }

    //AvailableFaces the lit faces of the state, maps to vanilla MultifaceBlock.availableFaces
    public static HashSet<Direction>? AvailableFaces(BlockState state)
    {
        if (!VegetationSupport.IsState(state, "sculk_vein")) return null;
        var faces = new HashSet<Direction>();
        foreach (var direction in Direction.Values)
            if (HasFace(state, direction)) faces.Add(direction);
        return faces;
    }

    //AllShuffled a shuffled copy of the six directions, maps to vanilla Direction.allShuffled
    public static List<Direction> AllShuffled(RandomSource random)
        => VegetationSupport.ShuffledCopy(Direction.Values, random);

    //CanAttachTo whether a sculk vein can attach to the face, maps to vanilla MultifaceBlock.canAttachTo
    public static bool CanAttachTo(WorldGenRegion level, BlockPos pos, Direction directionTowardsNeighbour)
    {
        var neighbourPos = pos.Offset(directionTowardsNeighbour);
        return VegetationSupport.IsFaceSturdy(GetState(level, neighbourPos), directionTowardsNeighbour.Opposite);
    }

    //CanAttachTo overload taking a known neighbor state
    public static bool CanAttachTo(BlockState neighbourState, Direction directionTowardsNeighbour)
        => VegetationSupport.IsFaceSturdy(neighbourState, directionTowardsNeighbour.Opposite);

    //IsWaterSource whether the state is a water source
    public static bool IsWaterSource(BlockState state)
        => state.Owner == WaterBlock && !state.FluidState.IsEmpty;

    //BetweenClosed iteration over a closed bounding box: z outer, y middle, x inner. maps to vanilla BlockPos.betweenClosed
    public static IEnumerable<BlockPos> BetweenClosed(BlockPos from, BlockPos to)
    {
        for (var z = from.Z; z <= to.Z; z++)
        for (var y = from.Y; y <= to.Y; y++)
        for (var x = from.X; x <= to.X; x++)
            yield return new BlockPos(x, y, z);
    }
}

//SpreadType how a sculk vein is placed, maps to vanilla MultifaceSpreader.SpreadType
internal enum SpreadType
{
    SamePosition,
    SamePlane,
    WrapAround,
}

//SpreadPos the coordinate and attach face of one placement, maps to vanilla MultifaceSpreader.SpreadPos
internal readonly record struct SpreadPos(BlockPos Pos, Direction Face);

//MultifaceSpreader multiface block spreader, maps to vanilla MultifaceSpreader
//Only the form used by sculk veins is kept; placement checks follow SculkVeinSpreaderConfig
internal sealed class MultifaceSpreader
{
    private static readonly SpreadType[] DefaultSpreadOrder =
    {
        SpreadType.SamePosition, SpreadType.SamePlane, SpreadType.WrapAround,
    };

    //SameSpace spreads only within the same cell, maps to vanilla SculkVeinBlock.sameSpaceSpreader
    public static readonly MultifaceSpreader SameSpace = new(new[] { SpreadType.SamePosition });

    //Vein tries same-cell, same-plane and corner-around, maps to vanilla SculkVeinBlock.veinSpreader
    public static readonly MultifaceSpreader Vein = new(DefaultSpreadOrder);

    private readonly SpreadType[] _spreadTypes;

    private MultifaceSpreader(SpreadType[] spreadTypes) => _spreadTypes = spreadTypes;

    //SpreadAll spreads from each lit face of the state in all six directions and returns the number of successful placements, maps to vanilla spreadAll
    public int SpreadAll(BlockState state, WorldGenRegion level, BlockPos pos, bool postProcess)
    {
        var count = 0;
        foreach (var faceDirection in Direction.Values)
        {
            if (!CanSpreadFrom(state, faceDirection)) continue;
            foreach (var spreadDirection in Direction.Values)
                if (SpreadFromFaceTowardDirection(state, level, pos, faceDirection, spreadDirection, postProcess))
                    count++;
        }
        return count;
    }

    //CanSpreadFrom whether the face can act as a starting point, maps to vanilla SpreadConfig.canSpreadFrom
    private static bool CanSpreadFrom(BlockState state, Direction face)
        => IsOtherBlockValidAsSource(state) || SculkSupport.HasFace(state, face);

    //IsOtherBlockValidAsSource any non-sculk-vein block can act as a source, maps to vanilla isOtherBlockValidAsSource
    private static bool IsOtherBlockValidAsSource(BlockState state)
        => !VegetationSupport.IsState(state, "sculk_vein");

    //SpreadFromFaceTowardDirection place once from a face toward a direction, maps to vanilla spreadFromFaceTowardDirection
    private bool SpreadFromFaceTowardDirection(BlockState state, WorldGenRegion level, BlockPos pos,
        Direction fromFace, Direction spreadDirection, bool postProcess)
    {
        var spreadPos = GetSpreadFromFaceTowardDirection(state, level, pos, fromFace, spreadDirection);
        return spreadPos is { } target && SpreadToFace(level, target, postProcess);
    }

    //GetSpreadFromFaceTowardDirection try each spread type for the first placeable spot, maps to the vanilla method of the same name
    private SpreadPos? GetSpreadFromFaceTowardDirection(BlockState state, WorldGenRegion level, BlockPos pos,
        Direction startingFace, Direction spreadDirection)
    {
        if (spreadDirection.AxisValue == startingFace.AxisValue) return null;
        if (!IsOtherBlockValidAsSource(state)
            && (!SculkSupport.HasFace(state, startingFace) || SculkSupport.HasFace(state, spreadDirection)))
            return null;
        foreach (var type in _spreadTypes)
        {
            var spreadPos = GetSpreadPos(type, pos, spreadDirection, startingFace);
            if (CanSpreadInto(level, pos, spreadPos)) return spreadPos;
        }
        return null;
    }

    //GetSpreadPos compute the target coordinate and attach face for a spread type, maps to vanilla SpreadType.getSpreadPos
    private static SpreadPos GetSpreadPos(SpreadType type, BlockPos pos, Direction spreadDirection,
        Direction fromFace)
        => type switch
        {
            SpreadType.SamePlane => new SpreadPos(pos.Offset(spreadDirection), fromFace),
            SpreadType.WrapAround => new SpreadPos(
                pos.Offset(spreadDirection).Offset(fromFace), spreadDirection.Opposite),
            _ => new SpreadPos(pos, spreadDirection),
        };

    //CanSpreadInto whether a vein can be placed at this position, maps to vanilla canSpreadInto
    private static bool CanSpreadInto(WorldGenRegion level, BlockPos sourcePos, SpreadPos spreadPos)
    {
        var existingState = SculkSupport.GetState(level, spreadPos.Pos);
        return VeinStateCanBeReplaced(level, sourcePos, spreadPos, existingState)
            && IsValidStateForPlacement(level, existingState, spreadPos.Pos, spreadPos.Face);
    }

    //VeinStateCanBeReplaced whether the target cell can be replaced by a vein, maps to vanilla SculkVeinSpreaderConfig.stateCanBeReplaced
    private static bool VeinStateCanBeReplaced(WorldGenRegion level, BlockPos sourcePos, SpreadPos spreadPos,
        BlockState existingState)
    {
        var placementPos = spreadPos.Pos;
        var placementDirection = spreadPos.Face;
        var againstState = SculkSupport.GetState(level, placementPos.Offset(placementDirection));
        if (VegetationSupport.IsState(againstState, "sculk") || VegetationSupport.IsState(againstState, "sculk_catalyst")
            || VegetationSupport.IsState(againstState, "moving_piston")) return false;
        if (sourcePos.AsVec3i().DistManhattan(placementPos.AsVec3i()) == 2)
        {
            var neighbourPos = sourcePos.Offset(placementDirection.Opposite);
            if (VegetationSupport.IsFaceSturdy(SculkSupport.GetState(level, neighbourPos), placementDirection))
                return false;
        }
        if ((existingState.FluidState.IsEmpty || existingState.Owner == SculkSupport.WaterBlock)
            && !VegetationSupport.InTag(existingState, SculkSupport.FireTag))
        {
            return existingState.Owner is BlockBehaviour behaviour && behaviour.CanBeReplaced
                || DefaultStateCanBeReplaced(existingState);
        }
        return false;
    }

    //DefaultStateCanBeReplaced air, sculk veins or water sources are replaceable, maps to vanilla DefaultSpreaderConfig.stateCanBeReplaced
    private static bool DefaultStateCanBeReplaced(BlockState existingState)
        => existingState.Owner.IsAir || VegetationSupport.IsState(existingState, "sculk_vein")
            || SculkSupport.IsWaterSource(existingState);

    //IsValidStateForPlacement whether a vein can attach to the face, maps to vanilla MultifaceBlock.isValidStateForPlacement
    private static bool IsValidStateForPlacement(WorldGenRegion level, BlockState oldState, BlockPos placementPos,
        Direction placementDirection)
    {
        if (VegetationSupport.IsState(oldState, "sculk_vein")
            && SculkSupport.HasFace(oldState, placementDirection)) return false;
        var neighbourPos = placementPos.Offset(placementDirection);
        return SculkSupport.CanAttachTo(SculkSupport.GetState(level, neighbourPos), placementDirection);
    }

    //SpreadToFace place the vein, maps to vanilla spreadToFace
    private static bool SpreadToFace(WorldGenRegion level, SpreadPos spreadPos, bool postProcess)
    {
        //Vanilla registers chunk post-processing when postProcess is true; the post-processing chain is not wired up here, so only the block is placed
        _ = postProcess;
        var oldState = SculkSupport.GetState(level, spreadPos.Pos);
        var newState = GetStateForPlacement(oldState, level, spreadPos.Pos, spreadPos.Face);
        if (newState is not { } placement) return false;
        SculkSupport.SetState(level, spreadPos.Pos, placement);
        return true;
    }

    //GetStateForPlacement derive the state after placing a vein from the old state, maps to vanilla MultifaceBlock.getStateForPlacement
    private static BlockState? GetStateForPlacement(BlockState oldState, WorldGenRegion level, BlockPos placementPos,
        Direction placementDirection)
    {
        if (!IsValidStateForPlacement(level, oldState, placementPos, placementDirection)) return null;
        BlockState newState;
        if (VegetationSupport.IsState(oldState, "sculk_vein")) newState = oldState;
        else if (SculkSupport.IsWaterSource(oldState))
            newState = VegetationSupport.WithProperty(VegetationSupport.StateOf("sculk_vein"), "waterlogged", true);
        else newState = VegetationSupport.StateOf("sculk_vein");
        return SculkSupport.WithFace(newState, placementDirection, true);
    }
}

//SculkBehaviourHandler behavior of sculk blocks, maps to the vanilla SculkBehaviour interface
//Only sculk blocks and sculk veins carry behavior during world generation; all other blocks use the default
internal abstract class SculkBehaviourHandler
{
    //Default behavior of non-sculk blocks, maps to vanilla SculkBehaviour.DEFAULT
    public static readonly SculkBehaviourHandler Default = new DefaultSculkBehaviour();

    //Sculk behavior of sculk blocks, maps to vanilla SculkBlock
    public static readonly SculkBehaviourHandler Sculk = new SculkBlockBehaviour();

    //Vein behavior of sculk veins, maps to vanilla SculkVeinBlock
    public static readonly SculkBehaviourHandler Vein = new SculkVeinBehaviour();

    //GetBlockBehaviour fetch the behavior for a block, maps to vanilla ChargeCursor.getBlockBehaviour
    public static SculkBehaviourHandler GetBlockBehaviour(BlockState state)
    {
        if (VegetationSupport.IsState(state, "sculk_vein")) return Vein;
        if (VegetationSupport.IsState(state, "sculk")) return Sculk;
        return Default;
    }

    //IsSculkBlock whether the block carries sculk behavior
    public static bool IsSculkBlock(BlockState state) => GetBlockBehaviour(state) != Default;

    //AttemptUseCharge settle one charge and return the remainder, maps to vanilla attemptUseCharge
    public virtual int AttemptUseCharge(ChargeCursor cursor, WorldGenRegion level, BlockPos originPos,
        RandomSource random, SculkSpreader spreader, bool spreadVeins) => cursor.Charge;

    //SculkSpreadDelay rounds to wait after this block spreads, maps to vanilla getSculkSpreadDelay
    public virtual int SculkSpreadDelay => 1;

    //OnDischarged cleanup when the charge runs out, maps to vanilla onDischarged
    public virtual void OnDischarged(WorldGenRegion level, BlockState state, BlockPos pos, RandomSource random) { }

    //AttemptSpreadVein try to spread veins, maps to vanilla attemptSpreadVein
    public virtual bool AttemptSpreadVein(WorldGenRegion level, BlockPos pos, BlockState state,
        HashSet<Direction>? facings, bool postProcess)
        => MultifaceSpreader.Vein.SpreadAll(state, level, pos, postProcess) > 0;

    //CanChangeBlockStateOnSpread whether the state must be re-read after spreading, maps to vanilla canChangeBlockStateOnSpread
    public virtual bool CanChangeBlockStateOnSpread => true;

    //UpdateDecayDelay decay delay for the next round, maps to vanilla updateDecayDelay
    public virtual int UpdateDecayDelay(int age) => 1;
}

//DefaultSculkBehaviour default behavior on non-sculk blocks, maps to vanilla SculkBehaviour.DEFAULT
internal sealed class DefaultSculkBehaviour : SculkBehaviourHandler
{
    public override bool AttemptSpreadVein(WorldGenRegion level, BlockPos pos, BlockState state,
        HashSet<Direction>? facings, bool postProcess)
    {
        if (facings is null) return MultifaceSpreader.SameSpace.SpreadAll(state, level, pos, postProcess) > 0;
        if (facings.Count > 0)
        {
            if (state.Owner.IsAir || state.Owner == SculkSupport.WaterBlock)
                return Regrow(level, pos, state, facings);
            return false;
        }
        return base.AttemptSpreadVein(level, pos, state, facings, postProcess);
    }

    public override int AttemptUseCharge(ChargeCursor cursor, WorldGenRegion level, BlockPos originPos,
        RandomSource random, SculkSpreader spreader, bool spreadVeins)
        => cursor.DecayDelay > 0 ? cursor.Charge : 0;

    public override int UpdateDecayDelay(int age) => Math.Max(age - 1, 0);

    //Regrow regrow one vein from the existing faces, maps to vanilla SculkVeinBlock.regrow
    private static bool Regrow(WorldGenRegion level, BlockPos pos, BlockState existing, HashSet<Direction> faces)
    {
        var hasAtLeastOneFace = false;
        var newState = VegetationSupport.StateOf("sculk_vein");
        foreach (var face in faces)
        {
            if (!SculkSupport.CanAttachTo(level, pos, face)) continue;
            newState = SculkSupport.WithFace(newState, face, true);
            hasAtLeastOneFace = true;
        }
        if (!hasAtLeastOneFace) return false;
        if (!existing.FluidState.IsEmpty)
            newState = VegetationSupport.WithProperty(newState, "waterlogged", true);
        SculkSupport.SetState(level, pos, newState);
        return true;
    }
}

//SculkBlockBehaviour sculk block behavior, maps to vanilla SculkBlock
//Grows sculk sensors or shriekers upward only when there is enough charge and enough distance from the origin
internal sealed class SculkBlockBehaviour : SculkBehaviourHandler
{
    public override bool CanChangeBlockStateOnSpread => false;

    public override int AttemptUseCharge(ChargeCursor cursor, WorldGenRegion level, BlockPos originPos,
        RandomSource random, SculkSpreader spreader, bool spreadVeins)
    {
        var charge = cursor.Charge;
        if (charge == 0 || random.NextInt(spreader.ChargeDecayRate) != 0) return charge;
        var chargePos = cursor.Pos;
        var isCloseToOrigin = chargePos.AsVec3i()
            .CloserThan(originPos.AsVec3i(), spreader.NoGrowthRadius);
        if (isCloseToOrigin || !CanPlaceGrowth(level, chargePos))
        {
            if (random.NextInt(spreader.AdditionalDecayRate) != 0) return charge;
            return charge - (isCloseToOrigin ? 1 : GetDecayPenalty(spreader, chargePos, originPos, charge));
        }
        var growthSpawnCost = spreader.GrowthSpawnCost;
        if (random.NextInt(growthSpawnCost) < charge)
        {
            var growthPlacement = chargePos.Offset(0, 1, 0);
            var growthState = GetRandomGrowthState(level, growthPlacement, random, spreader.IsWorldGeneration);
            SculkSupport.SetState(level, growthPlacement, growthState);
        }
        return Math.Max(0, charge - growthSpawnCost);
    }

    //GetDecayPenalty the farther from the origin the larger the penalty, maps to vanilla getDecayPenalty
    private static int GetDecayPenalty(SculkSpreader spreader, BlockPos pos, BlockPos originPos, int charge)
    {
        var noGrowthRadius = spreader.NoGrowthRadius;
        var outerDistanceSquared = Mth.Square(
            (float)Math.Sqrt(pos.AsVec3i().DistSqr(originPos.AsVec3i())) - noGrowthRadius);
        var maxReachSquared = Mth.Square(24 - noGrowthRadius);
        var distanceFactor = Math.Min(1.0f, outerDistanceSquared / maxReachSquared);
        return Math.Max(1, (int)(charge * distanceFactor * 0.5f));
    }

    //GetRandomGrowthState one in eleven grows a shrieker, the rest grow sensors, maps to vanilla getRandomGrowthState
    private static BlockState GetRandomGrowthState(WorldGenRegion level, BlockPos pos, RandomSource random,
        bool isWorldGen)
    {
        var state = random.NextInt(11) == 0
            ? VegetationSupport.WithProperty(VegetationSupport.StateOf("sculk_shrieker"), "can_summon", isWorldGen)
            : VegetationSupport.StateOf("sculk_sensor");
        if (VegetationSupport.HasProperty(state, "waterlogged")
            && !SculkSupport.GetState(level, pos).FluidState.IsEmpty)
            return VegetationSupport.WithProperty(state, "waterlogged", true);
        return state;
    }

    //CanPlaceGrowth air above and no more than two nearby sensors, maps to vanilla canPlaceGrowth
    private static bool CanPlaceGrowth(WorldGenRegion level, BlockPos pos)
    {
        var stateAbove = SculkSupport.GetState(level, pos.Offset(0, 1, 0));
        if (!stateAbove.Owner.IsAir
            && (stateAbove.Owner != SculkSupport.WaterBlock || stateAbove.FluidState.IsEmpty)) return false;
        var growthCount = 0;
        foreach (var blockPos in SculkSupport.BetweenClosed(
                     pos.Offset(-4, 0, -4), pos.Offset(4, 2, 4)))
        {
            var state = SculkSupport.GetState(level, blockPos);
            if (VegetationSupport.IsState(state, "sculk_sensor")
                || VegetationSupport.IsState(state, "sculk_shrieker")) growthCount++;
            if (growthCount > 2) return false;
        }
        return true;
    }
}

//SculkVeinBehaviour sculk vein behavior, maps to vanilla SculkVeinBlock
//When a vein touches a replaceable block it turns it into a sculk block and spreads the vein outward
internal sealed class SculkVeinBehaviour : SculkBehaviourHandler
{
    public override int AttemptUseCharge(ChargeCursor cursor, WorldGenRegion level, BlockPos originPos,
        RandomSource random, SculkSpreader spreader, bool spreadVeins)
    {
        if (spreadVeins && AttemptPlaceSculk(spreader, level, cursor.Pos, random)) return cursor.Charge - 1;
        return random.NextInt(spreader.ChargeDecayRate) == 0 ? Mth.Floor(cursor.Charge * 0.5f) : cursor.Charge;
    }

    public override void OnDischarged(WorldGenRegion level, BlockState state, BlockPos pos, RandomSource random)
    {
        if (!VegetationSupport.IsState(state, "sculk_vein")) return;
        foreach (var direction in Direction.Values)
        {
            if (!SculkSupport.HasFace(state, direction)) continue;
            if (VegetationSupport.IsState(SculkSupport.GetState(level, pos.Offset(direction)), "sculk"))
                state = SculkSupport.WithFace(state, direction, false);
        }
        if (!SculkSupport.HasAnyFace(state))
        {
            var fluidState = SculkSupport.GetState(level, pos).FluidState;
            state = fluidState.IsEmpty
                ? VegetationSupport.StateOf("air")
                : VegetationSupport.StateOf("water");
        }
        SculkSupport.SetState(level, pos, state);
    }

    //AttemptPlaceSculk try to turn replaceable blocks on the attach faces into sculk blocks, maps to vanilla attemptPlaceSculk
    private static bool AttemptPlaceSculk(SculkSpreader spreader, WorldGenRegion level, BlockPos pos,
        RandomSource random)
    {
        var state = SculkSupport.GetState(level, pos);
        var replaceTag = spreader.ReplaceableBlocks;
        foreach (var support in SculkSupport.AllShuffled(random))
        {
            if (!SculkSupport.HasFace(state, support)) continue;
            var supportPos = pos.Offset(support);
            var supportState = SculkSupport.GetState(level, supportPos);
            if (!VegetationSupport.InTag(supportState, replaceTag)) continue;
            var defaultSculk = VegetationSupport.StateOf("sculk");
            SculkSupport.SetState(level, supportPos, defaultSculk);
            MultifaceSpreader.Vein.SpreadAll(defaultSculk, level, supportPos, spreader.IsWorldGeneration);
            var skip = support.Opposite;
            foreach (var veinBlocks in Direction.Values)
            {
                if (veinBlocks == skip) continue;
                var veinPos = supportPos.Offset(veinBlocks);
                var possibleVein = SculkSupport.GetState(level, veinPos);
                if (VegetationSupport.IsState(possibleVein, "sculk_vein"))
                    Vein.OnDischarged(level, possibleVein, veinPos, random);
            }
            return true;
        }
        return false;
    }

    //HasSubstrateAccess whether any vein face touches a replaceable block, maps to vanilla hasSubstrateAccess
    public static bool HasSubstrateAccess(WorldGenRegion level, BlockState state, BlockPos pos)
    {
        if (!VegetationSupport.IsState(state, "sculk_vein")) return false;
        foreach (var direction in Direction.Values)
            if (SculkSupport.HasFace(state, direction)
                && VegetationSupport.InTag(SculkSupport.GetState(level, pos.Offset(direction)),
                    SculkSupport.ReplaceableTag)) return true;
        return false;
    }
}

//SculkSpreader sculk charge cursor manager, maps to vanilla SculkSpreader
//World generation uses growth cost 50, no-growth radius 1, decay rate 5 and extra decay rate 10
internal sealed class SculkSpreader
{
    private readonly List<ChargeCursor> _cursors = new();

    public static SculkSpreader CreateWorldGenSpreader()
        => new(true, SculkSupport.ReplaceableWorldGenTag, 50, 1, 5, 10);

    public SculkSpreader(bool isWorldGeneration, TagKey<RegBlock> replaceableBlocks, int growthSpawnCost,
        int noGrowthRadius, int chargeDecayRate, int additionalDecayRate)
    {
        IsWorldGeneration = isWorldGeneration;
        ReplaceableBlocks = replaceableBlocks;
        GrowthSpawnCost = growthSpawnCost;
        NoGrowthRadius = noGrowthRadius;
        ChargeDecayRate = chargeDecayRate;
        AdditionalDecayRate = additionalDecayRate;
    }

    public bool IsWorldGeneration { get; }
    public TagKey<RegBlock> ReplaceableBlocks { get; }
    public int GrowthSpawnCost { get; }
    public int NoGrowthRadius { get; }
    public int ChargeDecayRate { get; }
    public int AdditionalDecayRate { get; }

    //Clear drop all cursors, maps to vanilla clear
    public void Clear() => _cursors.Clear();

    //AddCursors scatter cursors by charge amount, capped at one thousand per cursor, maps to vanilla addCursors
    public void AddCursors(BlockPos startPos, int charge)
    {
        while (charge > 0)
        {
            var currentCharge = Math.Min(charge, ChargeCursor.MaxCharge);
            AddCursor(new ChargeCursor(startPos, currentCharge));
            charge -= currentCharge;
        }
    }

    //AddCursor cursor count cap of thirty-two, maps to vanilla addCursor
    private void AddCursor(ChargeCursor cursor)
    {
        if (_cursors.Count >= 32) return;
        _cursors.Add(cursor);
    }

    //UpdateCursors advance each cursor and merge those in the same cell, maps to vanilla updateCursors
    public void UpdateCursors(WorldGenRegion level, BlockPos originPos, RandomSource random, bool spreadVeins)
    {
        if (_cursors.Count == 0) return;
        var processedCursors = new List<ChargeCursor>();
        var mergeableCursors = new Dictionary<BlockPos, ChargeCursor>();
        foreach (var cursor in _cursors)
        {
            if (cursor.IsPosUnreasonable(originPos)) continue;
            cursor.Update(level, originPos, random, this, spreadVeins);
            if (cursor.Charge <= 0) continue;
            var pos = cursor.Pos;
            if (!mergeableCursors.TryGetValue(pos, out var existing))
            {
                mergeableCursors[pos] = cursor;
                processedCursors.Add(cursor);
            }
            else if (!IsWorldGeneration && cursor.Charge + existing.Charge <= ChargeCursor.MaxCharge)
            {
                existing.MergeWith(cursor);
            }
            else
            {
                processedCursors.Add(cursor);
                if (cursor.Charge < existing.Charge) mergeableCursors[pos] = cursor;
            }
        }
        _cursors.Clear();
        _cursors.AddRange(processedCursors);
    }
}

//ChargeCursor charge-carrying cursor, maps to vanilla SculkSpreader.ChargeCursor
internal sealed class ChargeCursor
{
    public const int MaxCharge = 1000;

    //NonCornerNeighbours the eighteen non-corner neighbor offsets, maps to vanilla NON_CORNER_NEIGHBOURS
    private static readonly List<Vec3i> NonCornerNeighbours = BuildNonCornerNeighbours();

    public BlockPos Pos { get; private set; }
    public int Charge { get; set; }
    public int UpdateDelay { get; private set; }
    public int DecayDelay { get; private set; }
    public HashSet<Direction>? Facings { get; private set; }

    public ChargeCursor(BlockPos pos, int charge)
    {
        Pos = pos;
        Charge = charge;
        DecayDelay = 1;
        UpdateDelay = 0;
    }

    //IsPosUnreasonable discard cursors that wander too far, maps to vanilla isPosUnreasonable
    public bool IsPosUnreasonable(BlockPos originPos)
    {
        var dx = Math.Abs(Pos.X - originPos.X);
        var dy = Math.Abs(Pos.Y - originPos.Y);
        var dz = Math.Abs(Pos.Z - originPos.Z);
        return Math.Max(dx, Math.Max(dy, dz)) > 1024;
    }

    //MergeWith merge in another cursor's charge, maps to vanilla mergeWith
    public void MergeWith(ChargeCursor other)
    {
        Charge += other.Charge;
        other.Charge = 0;
        UpdateDelay = Math.Min(UpdateDelay, other.UpdateDelay);
    }

    //Update advance one round, maps to vanilla update
    //Random consumption order, vein spreading, charge settlement and finding the next position all follow vanilla
    public void Update(WorldGenRegion level, BlockPos originPos, RandomSource random, SculkSpreader spreader,
        bool spreadVeins)
    {
        if (!ShouldUpdate(spreader.IsWorldGeneration)) return;
        if (UpdateDelay > 0)
        {
            UpdateDelay--;
            return;
        }
        var currentState = SculkSupport.GetState(level, Pos);
        var behaviour = SculkBehaviourHandler.GetBlockBehaviour(currentState);
        if (spreadVeins
            && behaviour.AttemptSpreadVein(level, Pos, currentState, Facings, spreader.IsWorldGeneration))
        {
            if (behaviour.CanChangeBlockStateOnSpread)
            {
                currentState = SculkSupport.GetState(level, Pos);
                behaviour = SculkBehaviourHandler.GetBlockBehaviour(currentState);
            }
        }
        Charge = behaviour.AttemptUseCharge(this, level, originPos, random, spreader, spreadVeins);
        if (Charge <= 0)
        {
            behaviour.OnDischarged(level, currentState, Pos, random);
            return;
        }
        if (GetValidMovementPos(level, Pos, random) is { } transferPos)
        {
            behaviour.OnDischarged(level, currentState, Pos, random);
            Pos = transferPos;
            if (spreader.IsWorldGeneration
                && !Pos.AsVec3i().CloserThan(new Vec3i(originPos.X, Pos.Y, originPos.Z), 15.0d))
            {
                Charge = 0;
                return;
            }
            currentState = SculkSupport.GetState(level, Pos);
        }
        if (SculkBehaviourHandler.IsSculkBlock(currentState)) Facings = SculkSupport.AvailableFaces(currentState);
        DecayDelay = behaviour.UpdateDecayDelay(DecayDelay);
        UpdateDelay = behaviour.SculkSpreadDelay;
    }

    //ShouldUpdate no update when the charge is spent; outside world generation the chunk must also be in tick range, maps to vanilla shouldUpdate
    //Only the world generation path is wired up here, so anything outside it is treated as out of tick range
    private bool ShouldUpdate(bool isWorldGeneration) => Charge > 0 && isWorldGeneration;

    //GetValidMovementPos find a landing spot with sculk behavior among the non-corner neighbors, maps to vanilla getValidMovementPos
    private static BlockPos? GetValidMovementPos(WorldGenRegion level, BlockPos pos, RandomSource random)
    {
        var sculkPosition = pos;
        foreach (var offset in VegetationSupport.ShuffledCopy(NonCornerNeighbours, random))
        {
            var neighbour = pos.Offset(offset);
            var transferee = SculkSupport.GetState(level, neighbour);
            if (!SculkBehaviourHandler.IsSculkBlock(transferee)
                || !IsMovementUnobstructed(level, pos, neighbour)) continue;
            sculkPosition = neighbour;
            if (SculkVeinBehaviour.HasSubstrateAccess(level, transferee, neighbour)) break;
        }
        return sculkPosition == pos ? null : sculkPosition;
    }

    //IsMovementUnobstructed diagonal movement requires the path to be clear, maps to vanilla isMovementUnobstructed
    private static bool IsMovementUnobstructed(WorldGenRegion level, BlockPos from, BlockPos to)
    {
        if (from.AsVec3i().DistManhattan(to.AsVec3i()) == 1) return true;
        var delta = new Vec3i(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
        var directionX = Direction.ByAxisDirection(Direction.Axis.X,
            delta.X < 0 ? Direction.AxisDirection.Negative : Direction.AxisDirection.Positive);
        var directionY = Direction.ByAxisDirection(Direction.Axis.Y,
            delta.Y < 0 ? Direction.AxisDirection.Negative : Direction.AxisDirection.Positive);
        var directionZ = Direction.ByAxisDirection(Direction.Axis.Z,
            delta.Z < 0 ? Direction.AxisDirection.Negative : Direction.AxisDirection.Positive);
        if (delta.X == 0) return IsUnobstructed(level, from, directionY) || IsUnobstructed(level, from, directionZ);
        if (delta.Y == 0) return IsUnobstructed(level, from, directionX) || IsUnobstructed(level, from, directionZ);
        return IsUnobstructed(level, from, directionX) || IsUnobstructed(level, from, directionY);
    }

    //IsUnobstructed the next cell along the direction is not a full face, maps to vanilla isUnobstructed
    private static bool IsUnobstructed(WorldGenRegion level, BlockPos from, Direction direction)
    {
        var testPos = from.Offset(direction);
        return !VegetationSupport.IsFaceSturdy(SculkSupport.GetState(level, testPos), direction.Opposite);
    }

    //BuildNonCornerNeighbours the non-corner, non-origin offsets in a 3x3x3 cube, maps to vanilla NON_CORNER_NEIGHBOURS
    //Ordered like BlockPos.betweenClosed: z outer, y middle, x inner
    private static List<Vec3i> BuildNonCornerNeighbours()
    {
        var list = new List<Vec3i>();
        for (var z = -1; z <= 1; z++)
        for (var y = -1; y <= 1; y++)
        for (var x = -1; x <= 1; x++)
        {
            if (x != 0 && y != 0 && z != 0) continue;
            if (x == 0 && y == 0 && z == 0) continue;
            list.Add(new Vec3i(x, y, z));
        }
        return list;
    }
}
