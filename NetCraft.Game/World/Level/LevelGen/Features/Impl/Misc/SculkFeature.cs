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

//SculkPatchConfiguration 幽匿斑块配置 对应原版 SculkPatchConfiguration
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

//SculkPatchFeature 幽匿斑块特征 对应原版 SculkPatchFeature
//从原点撒若干携带电荷的游标 每轮让它们把附近的方块换成幽匿脉络或幽匿块
//散播轮数与尝试次数决定随机消耗次数 数目对不上同种子铺出的斑块就不同
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

    //CanSpreadFrom 该格能不能作为散播起点 对应原版 canSpreadFrom
    //起点是幽匿方块 或者是空气水源且六个方向有整面实心 才算能长
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

    //IsCollisionShapeFullBlock 该状态碰撞形状是否占满整格
    private static bool IsCollisionShapeFullBlock(BlockState state, BlockPos pos)
        => state.Owner is BlockBehaviour behaviour
            && behaviour.IsCollisionShapeFullBlock(state, EmptyBlockGetter.Instance, pos);
}

//SculkSupport 幽匿子系统共用的方块判定与标签 对应原版散落各处的静态成员
internal static class SculkSupport
{
    //SculkReplaceableWorldGenTag 世界生成可被幽匿替换的方块 对应原版 BlockTags.SCULK_REPLACEABLE_WORLD_GEN
    public static readonly TagKey<RegBlock> ReplaceableWorldGenTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("sculk_replaceable_world_gen"));

    //SculkReplaceableTag 可被幽匿替换的方块 对应原版 BlockTags.SCULK_REPLACEABLE
    public static readonly TagKey<RegBlock> ReplaceableTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("sculk_replaceable"));

    //FireTag 火焰方块 幽匿脉络不长在火上 对应原版 BlockTags.FIRE
    public static readonly TagKey<RegBlock> FireTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("fire"));

    //WaterBlock 水方块 流体状态体系只保留有无 世界生成里水恒为源
    public static readonly RegBlock WaterBlock = VegetationSupport.BlockOf("water");

    //GetState 读世界里的方块状态
    public static BlockState GetState(WorldGenRegion level, BlockPos pos)
        => level.GetBlockState(pos.X, pos.Y, pos.Z);

    //SetState 写方块状态
    public static void SetState(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.SetBlockState(pos.X, pos.Y, pos.Z, state);

    //HasFace 该状态某个面是否被点亮 对应原版 MultifaceBlock.hasFace
    public static bool HasFace(BlockState state, Direction face)
    {
        var name = VegetationSupport.FaceName(face);
        foreach (var property in state.GetProperties())
            if (property.Name == name && property is BooleanProperty boolProperty)
                return state.GetValueOrElse(boolProperty, false);
        return false;
    }

    //WithFace 改某个面的点亮状态 对应原版 setValue(getFaceProperty(...))
    public static BlockState WithFace(BlockState state, Direction face, bool value)
        => VegetationSupport.WithProperty(state, VegetationSupport.FaceName(face), value);

    //HasAnyFace 至少有一个面被点亮 对应原版 MultifaceBlock.hasAnyFace
    public static bool HasAnyFace(BlockState state)
    {
        foreach (var direction in Direction.Values)
            if (HasFace(state, direction)) return true;
        return false;
    }

    //AvailableFaces 该状态点亮的面 对应原版 MultifaceBlock.availableFaces
    public static HashSet<Direction>? AvailableFaces(BlockState state)
    {
        if (!VegetationSupport.IsState(state, "sculk_vein")) return null;
        var faces = new HashSet<Direction>();
        foreach (var direction in Direction.Values)
            if (HasFace(state, direction)) faces.Add(direction);
        return faces;
    }

    //AllShuffled 六个方向的洗牌副本 对应原版 Direction.allShuffled
    public static List<Direction> AllShuffled(RandomSource random)
        => VegetationSupport.ShuffledCopy(Direction.Values, random);

    //CanAttachTo 该面能否贴上幽匿脉络 对应原版 MultifaceBlock.canAttachTo
    public static bool CanAttachTo(WorldGenRegion level, BlockPos pos, Direction directionTowardsNeighbour)
    {
        var neighbourPos = pos.Offset(directionTowardsNeighbour);
        return VegetationSupport.IsFaceSturdy(GetState(level, neighbourPos), directionTowardsNeighbour.Opposite);
    }

    //CanAttachTo 邻居状态已知时的重载
    public static bool CanAttachTo(BlockState neighbourState, Direction directionTowardsNeighbour)
        => VegetationSupport.IsFaceSturdy(neighbourState, directionTowardsNeighbour.Opposite);

    //IsWaterSource 该状态是不是水源
    public static bool IsWaterSource(BlockState state)
        => state.Owner == WaterBlock && !state.FluidState.IsEmpty;

    //BetweenClosed 闭区间长方体遍历 z 外层 y 中间 x 内层 对应原版 BlockPos.betweenClosed
    public static IEnumerable<BlockPos> BetweenClosed(BlockPos from, BlockPos to)
    {
        for (var z = from.Z; z <= to.Z; z++)
        for (var y = from.Y; y <= to.Y; y++)
        for (var x = from.X; x <= to.X; x++)
            yield return new BlockPos(x, y, z);
    }
}

//SpreadType 幽匿脉络的落位方式 对应原版 MultifaceSpreader.SpreadType
internal enum SpreadType
{
    SamePosition,
    SamePlane,
    WrapAround,
}

//SpreadPos 一次落位的坐标与贴附面 对应原版 MultifaceSpreader.SpreadPos
internal readonly record struct SpreadPos(BlockPos Pos, Direction Face);

//MultifaceSpreader 多面方块散布器 对应原版 MultifaceSpreader
//只保留幽匿脉络用到的形态 落位判定按 SculkVeinSpreaderConfig 走
internal sealed class MultifaceSpreader
{
    private static readonly SpreadType[] DefaultSpreadOrder =
    {
        SpreadType.SamePosition, SpreadType.SamePlane, SpreadType.WrapAround,
    };

    //SameSpace 只往同一格铺 对应原版 SculkVeinBlock.sameSpaceSpreader
    public static readonly MultifaceSpreader SameSpace = new(new[] { SpreadType.SamePosition });

    //Vein 同一格 同平面 绕角三种都试 对应原版 SculkVeinBlock.veinSpreader
    public static readonly MultifaceSpreader Vein = new(DefaultSpreadOrder);

    private readonly SpreadType[] _spreadTypes;

    private MultifaceSpreader(SpreadType[] spreadTypes) => _spreadTypes = spreadTypes;

    //SpreadAll 从该状态的每个点亮面朝六个方向铺一遍 返回成功落位次数 对应原版 spreadAll
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

    //CanSpreadFrom 该面能不能作为出发点 对应原版 SpreadConfig.canSpreadFrom
    private static bool CanSpreadFrom(BlockState state, Direction face)
        => IsOtherBlockValidAsSource(state) || SculkSupport.HasFace(state, face);

    //IsOtherBlockValidAsSource 非幽匿脉络方块都可当源 对应原版 isOtherBlockValidAsSource
    private static bool IsOtherBlockValidAsSource(BlockState state)
        => !VegetationSupport.IsState(state, "sculk_vein");

    //SpreadFromFaceTowardDirection 从某个面朝某方向落一次 对应原版 spreadFromFaceTowardDirection
    private bool SpreadFromFaceTowardDirection(BlockState state, WorldGenRegion level, BlockPos pos,
        Direction fromFace, Direction spreadDirection, bool postProcess)
    {
        var spreadPos = GetSpreadFromFaceTowardDirection(state, level, pos, fromFace, spreadDirection);
        return spreadPos is { } target && SpreadToFace(level, target, postProcess);
    }

    //GetSpreadFromFaceTowardDirection 逐种落位方式找第一个能放的位置 对应原版同名方法
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

    //GetSpreadPos 按落位方式算出目标坐标与贴附面 对应原版 SpreadType.getSpreadPos
    private static SpreadPos GetSpreadPos(SpreadType type, BlockPos pos, Direction spreadDirection,
        Direction fromFace)
        => type switch
        {
            SpreadType.SamePlane => new SpreadPos(pos.Offset(spreadDirection), fromFace),
            SpreadType.WrapAround => new SpreadPos(
                pos.Offset(spreadDirection).Offset(fromFace), spreadDirection.Opposite),
            _ => new SpreadPos(pos, spreadDirection),
        };

    //CanSpreadInto 该位置能否被铺上脉络 对应原版 canSpreadInto
    private static bool CanSpreadInto(WorldGenRegion level, BlockPos sourcePos, SpreadPos spreadPos)
    {
        var existingState = SculkSupport.GetState(level, spreadPos.Pos);
        return VeinStateCanBeReplaced(level, sourcePos, spreadPos, existingState)
            && IsValidStateForPlacement(level, existingState, spreadPos.Pos, spreadPos.Face);
    }

    //VeinStateCanBeReplaced 目标格能否被脉络替换 对应原版 SculkVeinSpreaderConfig.stateCanBeReplaced
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

    //DefaultStateCanBeReplaced 空气 幽匿脉络 或水源可被替换 对应原版 DefaultSpreaderConfig.stateCanBeReplaced
    private static bool DefaultStateCanBeReplaced(BlockState existingState)
        => existingState.Owner.IsAir || VegetationSupport.IsState(existingState, "sculk_vein")
            || SculkSupport.IsWaterSource(existingState);

    //IsValidStateForPlacement 该面能否贴脉络 对应原版 MultifaceBlock.isValidStateForPlacement
    private static bool IsValidStateForPlacement(WorldGenRegion level, BlockState oldState, BlockPos placementPos,
        Direction placementDirection)
    {
        if (VegetationSupport.IsState(oldState, "sculk_vein")
            && SculkSupport.HasFace(oldState, placementDirection)) return false;
        var neighbourPos = placementPos.Offset(placementDirection);
        return SculkSupport.CanAttachTo(SculkSupport.GetState(level, neighbourPos), placementDirection);
    }

    //SpreadToFace 把脉络放上去 对应原版 spreadToFace
    private static bool SpreadToFace(WorldGenRegion level, SpreadPos spreadPos, bool postProcess)
    {
        //原版 postProcess 为真时登记区块后处理 本项目后处理链未接 这里只落方块
        _ = postProcess;
        var oldState = SculkSupport.GetState(level, spreadPos.Pos);
        var newState = GetStateForPlacement(oldState, level, spreadPos.Pos, spreadPos.Face);
        if (newState is not { } placement) return false;
        SculkSupport.SetState(level, spreadPos.Pos, placement);
        return true;
    }

    //GetStateForPlacement 由旧状态推出放了脉络后的状态 对应原版 MultifaceBlock.getStateForPlacement
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

//SculkBehaviourHandler 幽匿方块的行为 对应原版 SculkBehaviour 接口
//世界生成里只有幽匿块与幽匿脉络两种方块带行为 其余方块走默认行为
internal abstract class SculkBehaviourHandler
{
    //Default 非幽匿方块的行为 对应原版 SculkBehaviour.DEFAULT
    public static readonly SculkBehaviourHandler Default = new DefaultSculkBehaviour();

    //Sculk 幽匿块的行为 对应原版 SculkBlock
    public static readonly SculkBehaviourHandler Sculk = new SculkBlockBehaviour();

    //Vein 幽匿脉络的行为 对应原版 SculkVeinBlock
    public static readonly SculkBehaviourHandler Vein = new SculkVeinBehaviour();

    //GetBlockBehaviour 按方块取行为 对应原版 ChargeCursor.getBlockBehaviour
    public static SculkBehaviourHandler GetBlockBehaviour(BlockState state)
    {
        if (VegetationSupport.IsState(state, "sculk_vein")) return Vein;
        if (VegetationSupport.IsState(state, "sculk")) return Sculk;
        return Default;
    }

    //IsSculkBlock 该方块是不是带幽匿行为的方块
    public static bool IsSculkBlock(BlockState state) => GetBlockBehaviour(state) != Default;

    //AttemptUseCharge 结算一次电荷 返回剩余电荷 对应原版 attemptUseCharge
    public virtual int AttemptUseCharge(ChargeCursor cursor, WorldGenRegion level, BlockPos originPos,
        RandomSource random, SculkSpreader spreader, bool spreadVeins) => cursor.Charge;

    //SculkSpreadDelay 本方块散播后的等待轮数 对应原版 getSculkSpreadDelay
    public virtual int SculkSpreadDelay => 1;

    //OnDischarged 电荷耗尽时的收尾 对应原版 onDischarged
    public virtual void OnDischarged(WorldGenRegion level, BlockState state, BlockPos pos, RandomSource random) { }

    //AttemptSpreadVein 试着把脉络铺开 对应原版 attemptSpreadVein
    public virtual bool AttemptSpreadVein(WorldGenRegion level, BlockPos pos, BlockState state,
        HashSet<Direction>? facings, bool postProcess)
        => MultifaceSpreader.Vein.SpreadAll(state, level, pos, postProcess) > 0;

    //CanChangeBlockStateOnSpread 铺开后要不要重新读状态 对应原版 canChangeBlockStateOnSpread
    public virtual bool CanChangeBlockStateOnSpread => true;

    //UpdateDecayDelay 下轮的衰减延迟 对应原版 updateDecayDelay
    public virtual int UpdateDecayDelay(int age) => 1;
}

//DefaultSculkBehaviour 非幽匿方块上的默认行为 对应原版 SculkBehaviour.DEFAULT
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

    //Regrow 按已有面重新长出一株脉络 对应原版 SculkVeinBlock.regrow
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

//SculkBlockBehaviour 幽匿块行为 对应原版 SculkBlock
//电荷够且离原点够远时才往上长幽匿感测体或尖啸体
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

    //GetDecayPenalty 离原点越远扣得越多 对应原版 getDecayPenalty
    private static int GetDecayPenalty(SculkSpreader spreader, BlockPos pos, BlockPos originPos, int charge)
    {
        var noGrowthRadius = spreader.NoGrowthRadius;
        var outerDistanceSquared = Mth.Square(
            (float)Math.Sqrt(pos.AsVec3i().DistSqr(originPos.AsVec3i())) - noGrowthRadius);
        var maxReachSquared = Mth.Square(24 - noGrowthRadius);
        var distanceFactor = Math.Min(1.0f, outerDistanceSquared / maxReachSquared);
        return Math.Max(1, (int)(charge * distanceFactor * 0.5f));
    }

    //GetRandomGrowthState 十一分之一长尖啸体 其余长感测体 对应原版 getRandomGrowthState
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

    //CanPlaceGrowth 上方空且附近感测体不超过两个 对应原版 canPlaceGrowth
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

//SculkVeinBehaviour 幽匿脉络行为 对应原版 SculkVeinBlock
//脉络挨着可替换方块时把它变成幽匿块并把脉络往外铺
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

    //AttemptPlaceSculk 试着把贴附面上的可替换方块换成幽匿块 对应原版 attemptPlaceSculk
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

    //HasSubstrateAccess 脉络是否有面贴着可替换方块 对应原版 hasSubstrateAccess
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

//SculkSpreader 幽匿电荷游标管理器 对应原版 SculkSpreader
//世界生成用的参数是 生长开销 50 禁长半径 1 衰减率 5 额外衰减率 10
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

    //Clear 清空全部游标 对应原版 clear
    public void Clear() => _cursors.Clear();

    //AddCursors 按电荷量撒游标 单个游标上限一千 对应原版 addCursors
    public void AddCursors(BlockPos startPos, int charge)
    {
        while (charge > 0)
        {
            var currentCharge = Math.Min(charge, ChargeCursor.MaxCharge);
            AddCursor(new ChargeCursor(startPos, currentCharge));
            charge -= currentCharge;
        }
    }

    //AddCursor 游标数上限三十二 对应原版 addCursor
    private void AddCursor(ChargeCursor cursor)
    {
        if (_cursors.Count >= 32) return;
        _cursors.Add(cursor);
    }

    //UpdateCursors 逐个推进游标并合并同格游标 对应原版 updateCursors
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

//ChargeCursor 携带电荷的游标 对应原版 SculkSpreader.ChargeCursor
internal sealed class ChargeCursor
{
    public const int MaxCharge = 1000;

    //NonCornerNeighbours 十八个非角邻居偏移 对应原版 NON_CORNER_NEIGHBOURS
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

    //IsPosUnreasonable 游标跑得太远直接丢弃 对应原版 isPosUnreasonable
    public bool IsPosUnreasonable(BlockPos originPos)
    {
        var dx = Math.Abs(Pos.X - originPos.X);
        var dy = Math.Abs(Pos.Y - originPos.Y);
        var dz = Math.Abs(Pos.Z - originPos.Z);
        return Math.Max(dx, Math.Max(dy, dz)) > 1024;
    }

    //MergeWith 合并另一个游标的电荷 对应原版 mergeWith
    public void MergeWith(ChargeCursor other)
    {
        Charge += other.Charge;
        other.Charge = 0;
        UpdateDelay = Math.Min(UpdateDelay, other.UpdateDelay);
    }

    //Update 推进一轮 对应原版 update
    //随机消耗顺序 铺脉络 结算电荷 找下个落点 全部照原版
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

    //ShouldUpdate 电荷耗尽不更新 非世界生成时还要区块在刻范围内 对应原版 shouldUpdate
    //本作接入的只有世界生成路径 非世界生成一律按不在刻范围内处理
    private bool ShouldUpdate(bool isWorldGeneration) => Charge > 0 && isWorldGeneration;

    //GetValidMovementPos 在非角邻居里找一个带幽匿行为的落点 对应原版 getValidMovementPos
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

    //IsMovementUnobstructed 斜向移动要求中间不被挡住 对应原版 isMovementUnobstructed
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

    //IsUnobstructed 沿该方向下一格不是整面 对应原版 isUnobstructed
    private static bool IsUnobstructed(WorldGenRegion level, BlockPos from, Direction direction)
    {
        var testPos = from.Offset(direction);
        return !VegetationSupport.IsFaceSturdy(SculkSupport.GetState(level, testPos), direction.Opposite);
    }

    //BuildNonCornerNeighbours 三乘三乘三里非角且非原点的偏移 对应原版 NON_CORNER_NEIGHBOURS
    //顺序按 BlockPos.betweenClosed 的 z 外层 y 中间 x 内层
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
