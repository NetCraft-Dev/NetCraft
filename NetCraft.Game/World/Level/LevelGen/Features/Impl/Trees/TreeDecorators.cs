using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using EnumDirection = NetCraft.Registry.Enums.Direction;
using PrimDirection = NetCraft.Primitives.Direction;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees;

//TreeDecoratorType tree decorator type base, maps to vanilla TreeDecoratorType<P>
public abstract class TreeDecoratorType : NetCraft.Registry.TreeDecoratorType<object>
{
    public Identifier Id { get; }

    protected TreeDecoratorType(Identifier id) => Id = id;

    public abstract DataResult<TreeDecorator> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    public abstract void EncodeFields<U>(DynamicOps<U> ops, TreeDecorator value, RecordBuilder<U> builder);
}

//TreeDecoratorType<P> generic middle layer for a concrete decorator type
public abstract class TreeDecoratorType<P> : TreeDecoratorType where P : TreeDecorator
{
    private readonly MapCodec<P> _codec;

    protected TreeDecoratorType(Identifier id, MapCodec<P> codec) : base(id) => _codec = codec;

    public sealed override DataResult<TreeDecorator> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(value => (TreeDecorator)value);

    public sealed override void EncodeFields<U>(DynamicOps<U> ops, TreeDecorator value, RecordBuilder<U> builder)
    {
        if (value is P typed) _codec.EncodeTo(ops, typed, builder);
    }
}

internal sealed class SimpleTreeDecoratorType<P> : TreeDecoratorType<P> where P : TreeDecorator
{
    public SimpleTreeDecoratorType(string id, MapCodec<P> codec)
        : base(Identifier.WithDefaultNamespace(id), codec) { }
}

//TreeDecoratorTypes built-in decorator type registration, maps to the static fields of vanilla TreeDecoratorType
public static class TreeDecoratorTypes
{
    public static readonly TreeDecoratorType<TrunkVineDecorator> TrunkVine =
        Register("trunk_vine", TrunkVineDecorator.Codec);

    public static readonly TreeDecoratorType<LeaveVineDecorator> LeaveVine =
        Register("leave_vine", LeaveVineDecorator.Codec);

    public static readonly TreeDecoratorType<CocoaDecorator> Cocoa =
        Register("cocoa", CocoaDecorator.Codec);

    public static readonly TreeDecoratorType<BeehiveDecorator> Beehive =
        Register("beehive", BeehiveDecorator.Codec);

    public static readonly TreeDecoratorType<AlterGroundDecorator> AlterGround =
        Register("alter_ground", AlterGroundDecorator.Codec);

    public static readonly TreeDecoratorType<AttachedToLeavesDecorator> AttachedToLeaves =
        Register("attached_to_leaves", AttachedToLeavesDecorator.Codec);

    public static readonly TreeDecoratorType<PlaceOnGroundDecorator> PlaceOnGround =
        Register("place_on_ground", PlaceOnGroundDecorator.Codec);

    public static readonly TreeDecoratorType<PaleMossDecorator> PaleMoss =
        Register("pale_moss", PaleMossDecorator.Codec);

    public static readonly TreeDecoratorType<CreakingHeartDecorator> CreakingHeart =
        Register("creaking_heart", CreakingHeartDecorator.Codec);

    private static TreeDecoratorType<T> Register<T>(string path, MapCodec<T> codec) where T : TreeDecorator
    {
        var type = new SimpleTreeDecoratorType<T>(path, codec);
        Registry<NetCraft.Registry.TreeDecoratorType<object>>.Register(
            BuiltInRegistries.TREE_DECORATOR_TYPE, type.Id, type);
        return type;
    }
}

//TreeDecoratorProperties block properties written by decorators; matched by name and value type against the block registry instances
internal static class TreeDecoratorProperties
{
    public static readonly BooleanProperty VineEast = new("east");
    public static readonly BooleanProperty VineWest = new("west");
    public static readonly BooleanProperty VineNorth = new("north");
    public static readonly BooleanProperty VineSouth = new("south");
    public static readonly IntegerProperty CocoaAge = new("age", 0, 2);
    //The field name avoids the enum type name so a member of the same name does not shadow the type
    public static readonly EnumProperty<CreakingHeartState> CreakingHeartStateProperty =
        new("creaking_heart_state", new[]
        {
            NetCraft.Registry.Enums.CreakingHeartState.uprooted,
            NetCraft.Registry.Enums.CreakingHeartState.dormant,
            NetCraft.Registry.Enums.CreakingHeartState.awake,
        });
    public static readonly BooleanProperty Natural = new("natural");

    //ToEnumDirection convert a game direction to the enum direction used by the facade property
    public static EnumDirection ToEnumDirection(PrimDirection direction) => direction.Id3D switch
    {
        PrimDirection.NorthId => EnumDirection.north,
        PrimDirection.EastId => EnumDirection.east,
        PrimDirection.SouthId => EnumDirection.south,
        _ => EnumDirection.west,
    };
}

//TreeDecorator tree decorator base, maps to vanilla TreeDecorator
public abstract class TreeDecorator
{
    public static readonly Codec<TreeDecorator> Codec = new TreeDecoratorDispatchCodec();

    public abstract TreeDecoratorType Type { get; }

    public abstract void Place(Context context);

    //Context decorator context handing the tree's placed positions and the random source to decorators, maps to vanilla TreeDecorator.Context
    public sealed class Context
    {
        private readonly WorldGenRegion _level;
        private readonly Action<BlockPos, BlockState> _decorationSetter;
        private readonly RandomSource _random;

        //Generator used by decorators to place embedded configured features, maps to vanilla context.level().getLevel().getChunkSource().getGenerator()
        public ChunkGenerator? Generator { get; }

        //Roots root positions, sorted by ascending Y
        public List<BlockPos> Roots { get; }

        //Logs trunk positions, sorted by ascending Y
        public List<BlockPos> Logs { get; }

        //Leaves leaf positions, sorted by ascending Y
        public List<BlockPos> Leaves { get; }

        public Context(WorldGenRegion level, Action<BlockPos, BlockState> decorationSetter, RandomSource random,
            ChunkGenerator? generator, IEnumerable<BlockPos> trunkSet, IEnumerable<BlockPos> foliageSet,
            IEnumerable<BlockPos> rootSet)
        {
            _level = level;
            _decorationSetter = decorationSetter;
            _random = random;
            Generator = generator;
            Roots = rootSet.OrderBy(pos => pos.Y).ToList();
            Logs = trunkSet.OrderBy(pos => pos.Y).ToList();
            Leaves = foliageSet.OrderBy(pos => pos.Y).ToList();
        }

        //PlaceVine place one vine attached only to the given face, maps to vanilla placeVine
        public void PlaceVine(BlockPos pos, BooleanProperty direction)
            => SetBlock(pos, TreeUtil.Vine.DefaultBlockState.SetValue(direction, true));

        public void SetBlock(BlockPos pos, BlockState state) => _decorationSetter(pos, state);

        public bool IsAir(BlockPos pos) => _level.GetBlockState(pos.X, pos.Y, pos.Z).Owner.IsAir;

        public bool CheckBlock(BlockPos pos, Func<BlockState, bool> predicate)
            => predicate(_level.GetBlockState(pos.X, pos.Y, pos.Z));

        public WorldGenRegion Level => _level;

        public RandomSource Random => _random;
    }
}

//TreeDecoratorDispatchCodec look up TREE_DECORATOR_TYPE by the type field then delegate to that type
internal sealed class TreeDecoratorDispatchCodec : ScalarCodec<TreeDecorator>
{
    public override DataResult<TreeDecorator> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeDecorator(ops, map));

    private static DataResult<TreeDecorator> DecodeDecorator<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<TreeDecorator>.Error(() => "tree decorator is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<TreeDecorator>.Error(() => "tree decorator type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<TreeDecorator>.Error(() => $"invalid decorator type: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.TREE_DECORATOR_TYPE.GetValue(typeId.Value) is not TreeDecoratorType type)
            return DataResult<TreeDecorator>.Error(() => $"unknown tree decorator type: {typeId}");
        return type.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, TreeDecorator value)
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Type.Id.ToString()));
        value.Type.EncodeFields(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}

//UnitMapCodec fieldless map codec, maps to vanilla MapCodec.unit
internal sealed class UnitMapCodec<T> : AbstractMapCodec<T>
{
    private readonly Func<T> _factory;

    public UnitMapCodec(Func<T> factory) => _factory = factory;

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<T>.Success(_factory());

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder) => builder;
}

//TrunkVineDecorator trunk vines, maps to vanilla TrunkVineDecorator
public sealed class TrunkVineDecorator : TreeDecorator
{
    //Instance declared first: static fields referenced by Codec must be initialized, otherwise nullability analysis reports a possible null reference
    public static readonly TrunkVineDecorator Instance = new();

    public static readonly MapCodec<TrunkVineDecorator> Codec = new UnitMapCodec<TrunkVineDecorator>(() => Instance);

    private TrunkVineDecorator() { }

    public override TreeDecoratorType Type => TreeDecoratorTypes.TrunkVine;

    public override void Place(Context context)
    {
        var random = context.Random;
        foreach (var pos in context.Logs)
        {
            if (random.NextInt(3) > 0)
            {
                var west = pos.Offset(-1, 0, 0);
                if (context.IsAir(west)) context.PlaceVine(west, TreeDecoratorProperties.VineEast);
            }
            if (random.NextInt(3) > 0)
            {
                var east = pos.Offset(1, 0, 0);
                if (context.IsAir(east)) context.PlaceVine(east, TreeDecoratorProperties.VineWest);
            }
            if (random.NextInt(3) > 0)
            {
                var north = pos.Offset(0, 0, -1);
                if (context.IsAir(north)) context.PlaceVine(north, TreeDecoratorProperties.VineSouth);
            }
            if (random.NextInt(3) > 0)
            {
                var south = pos.Offset(0, 0, 1);
                if (context.IsAir(south)) context.PlaceVine(south, TreeDecoratorProperties.VineNorth);
            }
        }
    }
}

//LeaveVineDecorator hanging leaf vines, maps to vanilla LeaveVineDecorator
public sealed class LeaveVineDecorator : TreeDecorator
{
    public static readonly MapCodec<LeaveVineDecorator> Codec =
        new SingleFieldMapCodec<LeaveVineDecorator, float>(
            Codecs.Float.FieldOf("probability"),
            probability => new LeaveVineDecorator(probability),
            decorator => decorator.Probability);

    public float Probability { get; }

    public LeaveVineDecorator(float probability) => Probability = probability;

    public override TreeDecoratorType Type => TreeDecoratorTypes.LeaveVine;

    public override void Place(Context context)
    {
        var random = context.Random;
        foreach (var pos in context.Leaves)
        {
            if (random.NextFloat() < Probability)
            {
                var west = pos.Offset(-1, 0, 0);
                if (context.IsAir(west)) AddHangingVine(west, TreeDecoratorProperties.VineEast, context);
            }
            if (random.NextFloat() < Probability)
            {
                var east = pos.Offset(1, 0, 0);
                if (context.IsAir(east)) AddHangingVine(east, TreeDecoratorProperties.VineWest, context);
            }
            if (random.NextFloat() < Probability)
            {
                var north = pos.Offset(0, 0, -1);
                if (context.IsAir(north)) AddHangingVine(north, TreeDecoratorProperties.VineSouth, context);
            }
            if (random.NextFloat() < Probability)
            {
                var south = pos.Offset(0, 0, 1);
                if (context.IsAir(south)) AddHangingVine(south, TreeDecoratorProperties.VineNorth, context);
            }
        }
    }

    //AddHangingVine hang up to four vine blocks downward from the position, maps to vanilla addHangingVine
    private static void AddHangingVine(BlockPos pos, BooleanProperty direction, Context context)
    {
        context.PlaceVine(pos, direction);
        var below = pos.Offset(0, -1, 0);
        for (var remaining = 4; context.IsAir(below) && remaining > 0; remaining--)
        {
            context.PlaceVine(below, direction);
            below = below.Offset(0, -1, 0);
        }
    }
}

//CocoaDecorator cocoa bean decorator, maps to vanilla CocoaDecorator
public sealed class CocoaDecorator : TreeDecorator
{
    public static readonly MapCodec<CocoaDecorator> Codec =
        new SingleFieldMapCodec<CocoaDecorator, float>(
            Codecs.Float.FieldOf("probability"),
            probability => new CocoaDecorator(probability),
            decorator => decorator.Probability);

    public float Probability { get; }

    public CocoaDecorator(float probability) => Probability = probability;

    public override TreeDecoratorType Type => TreeDecoratorTypes.Cocoa;

    public override void Place(Context context)
    {
        var random = context.Random;
        if (random.NextFloat() >= Probability) return;
        var logs = context.Logs;
        if (logs.Count == 0) return;
        var treeY = logs[0].Y;
        foreach (var pos in logs)
        {
            if (pos.Y - treeY > 2) continue;
            foreach (var direction in TreeUtil.Horizontal)
            {
                if (random.NextFloat() > 0.25f) continue;
                var opposite = direction.Opposite;
                var cocoaPos = pos.Offset(opposite.StepX, 0, opposite.StepZ);
                if (!context.IsAir(cocoaPos)) continue;
                var state = TreeUtil.Cocoa.DefaultBlockState
                    .SetValue(TreeDecoratorProperties.CocoaAge, random.NextInt(3))
                    .SetValue(BlockStateProperties.HorizontalFacing,
                        TreeDecoratorProperties.ToEnumDirection(direction));
                context.SetBlock(cocoaPos, state);
            }
        }
    }
}

//BeehiveDecorator beehive decorator, maps to vanilla BeehiveDecorator
//Vanilla also stores bees in the beehive block entity; block entity data is not wired up here, so only the beehive block itself is placed
public sealed class BeehiveDecorator : TreeDecorator
{
    public static readonly MapCodec<BeehiveDecorator> Codec =
        new SingleFieldMapCodec<BeehiveDecorator, float>(
            Codecs.Float.FieldOf("probability"),
            probability => new BeehiveDecorator(probability),
            decorator => decorator.Probability);

    private static readonly PrimDirection WorldgenFacing = PrimDirection.South;

    private static readonly PrimDirection[] SpawnDirections =
    {
        PrimDirection.East, PrimDirection.South, PrimDirection.West,
    };

    public float Probability { get; }

    public BeehiveDecorator(float probability) => Probability = probability;

    public override TreeDecoratorType Type => TreeDecoratorTypes.Beehive;

    public override void Place(Context context)
    {
        var leaves = context.Leaves;
        var logs = context.Logs;
        if (logs.Count == 0) return;
        var random = context.Random;
        if (random.NextFloat() >= Probability) return;
        var hiveY = leaves.Count > 0
            ? Math.Max(leaves[0].Y - 1, logs[0].Y + 1)
            : Math.Min(logs[0].Y + 1 + random.NextInt(3), logs[^1].Y);
        var hivePlacements = new List<BlockPos>();
        foreach (var pos in logs)
        {
            if (pos.Y != hiveY) continue;
            foreach (var spawn in SpawnDirections)
                hivePlacements.Add(pos.Offset(spawn));
        }
        if (hivePlacements.Count == 0) return;
        TreeUtil.Shuffle(hivePlacements, random);
        foreach (var candidate in hivePlacements)
        {
            if (!context.IsAir(candidate) || !context.IsAir(candidate.Offset(WorldgenFacing))) continue;
            context.SetBlock(candidate, TreeUtil.BeeNest.DefaultBlockState
                .SetValue(BlockStateProperties.HorizontalFacing,
                    TreeDecoratorProperties.ToEnumDirection(WorldgenFacing)));
            return;
        }
    }
}

//AlterGroundDecorator alters the ground around a tree, maps to vanilla AlterGroundDecorator
public sealed class AlterGroundDecorator : TreeDecorator
{
    public static readonly MapCodec<AlterGroundDecorator> Codec =
        new SingleFieldMapCodec<AlterGroundDecorator, BlockStateProvider>(
            BlockStateProvider.Codec.FieldOf("provider"),
            provider => new AlterGroundDecorator(provider),
            decorator => decorator.Provider);

    public BlockStateProvider Provider { get; }

    public AlterGroundDecorator(BlockStateProvider provider) => Provider = provider;

    public override TreeDecoratorType Type => TreeDecoratorTypes.AlterGround;

    public override void Place(Context context)
    {
        var blockPositions = TreeFeature.GetLowestTrunkOrRootOfTree(context);
        if (blockPositions.Count == 0) return;
        var minY = blockPositions[0].Y;
        foreach (var pos in blockPositions)
        {
            if (pos.Y != minY) continue;
            PlaceCircle(context, pos.Offset(-1, 0, -1));
            PlaceCircle(context, pos.Offset(2, 0, -1));
            PlaceCircle(context, pos.Offset(-1, 0, 2));
            PlaceCircle(context, pos.Offset(2, 0, 2));
            for (var i = 0; i < 5; i++)
            {
                var placement = context.Random.NextInt(64);
                var xx = placement % 8;
                var zz = placement / 8;
                if (xx == 0 || xx == 7 || zz == 0 || zz == 7)
                    PlaceCircle(context, pos.Offset(-3 + xx, 0, -3 + zz));
            }
        }
    }

    //PlaceCircle lay a 5x5 ground patch with the corners removed, maps to vanilla placeCircle
    private void PlaceCircle(Context context, BlockPos pos)
    {
        for (var xx = -2; xx <= 2; xx++)
        {
            for (var zz = -2; zz <= 2; zz++)
            {
                if (Math.Abs(xx) == 2 && Math.Abs(zz) == 2) continue;
                PlaceBlockAt(context, pos.Offset(xx, 0, zz));
            }
        }
    }

    //PlaceBlockAt search downward for the first alterable ground, maps to vanilla placeBlockAt
    private void PlaceBlockAt(Context context, BlockPos pos)
    {
        for (var dy = 2; dy >= -3; dy--)
        {
            var cursor = pos.Offset(0, dy, 0);
            var replaceWith = Provider.GetOptionalState(context.Level, context.Random, cursor);
            if (replaceWith is { } state)
            {
                context.SetBlock(cursor, state);
                return;
            }
            if (!context.IsAir(cursor) && dy < 0) return;
        }
    }
}

//AttachedToLeavesDecorator hangs fruit under leaves, used by mangrove propagules, maps to vanilla AttachedToLeavesDecorator
public sealed class AttachedToLeavesDecorator : TreeDecorator
{
    public static readonly MapCodec<AttachedToLeavesDecorator> Codec =
        RecordCodecBuilder.Of6<AttachedToLeavesDecorator, float, int, int, BlockStateProvider, int,
            IReadOnlyList<PrimDirection>>(
            Codecs.Float.FieldOf("probability")
                .ForGetter<AttachedToLeavesDecorator, float>(d => d.Probability),
            Codecs.Int.FieldOf("exclusion_radius_xz")
                .ForGetter<AttachedToLeavesDecorator, int>(d => d.ExclusionRadiusXZ),
            Codecs.Int.FieldOf("exclusion_radius_y")
                .ForGetter<AttachedToLeavesDecorator, int>(d => d.ExclusionRadiusY),
            BlockStateProvider.Codec.FieldOf("block_provider")
                .ForGetter<AttachedToLeavesDecorator, BlockStateProvider>(d => d.BlockProvider),
            Codecs.Int.FieldOf("required_empty_blocks")
                .ForGetter<AttachedToLeavesDecorator, int>(d => d.RequiredEmptyBlocks),
            DirectionCodec.Instance.ListOf().FieldOf("directions")
                .ForGetter<AttachedToLeavesDecorator, IReadOnlyList<PrimDirection>>(d => d.Directions),
            (probability, exclusionRadiusXZ, exclusionRadiusY, blockProvider, requiredEmptyBlocks, directions) =>
                new AttachedToLeavesDecorator(probability, exclusionRadiusXZ, exclusionRadiusY, blockProvider,
                    requiredEmptyBlocks, directions));

    public float Probability { get; }
    public int ExclusionRadiusXZ { get; }
    public int ExclusionRadiusY { get; }
    public BlockStateProvider BlockProvider { get; }
    public int RequiredEmptyBlocks { get; }
    public IReadOnlyList<PrimDirection> Directions { get; }

    public AttachedToLeavesDecorator(float probability, int exclusionRadiusXZ, int exclusionRadiusY,
        BlockStateProvider blockProvider, int requiredEmptyBlocks, IReadOnlyList<PrimDirection> directions)
    {
        Probability = probability;
        ExclusionRadiusXZ = exclusionRadiusXZ;
        ExclusionRadiusY = exclusionRadiusY;
        BlockProvider = blockProvider;
        RequiredEmptyBlocks = requiredEmptyBlocks;
        Directions = directions;
    }

    public override TreeDecoratorType Type => TreeDecoratorTypes.AttachedToLeaves;

    public override void Place(Context context)
    {
        var propaguleBlacklist = new HashSet<BlockPos>();
        var random = context.Random;
        foreach (var leafPos in TreeUtil.ShuffledCopy(context.Leaves, random))
        {
            var direction = TreeUtil.GetRandom(Directions, random);
            var placementPos = leafPos.Offset(direction);
            if (propaguleBlacklist.Contains(placementPos) || !(random.NextFloat() < Probability)
                || !HasRequiredEmptyBlocks(context, leafPos, direction))
                continue;
            var corner1 = placementPos.Offset(-ExclusionRadiusXZ, -ExclusionRadiusY, -ExclusionRadiusXZ);
            var corner2 = placementPos.Offset(ExclusionRadiusXZ, ExclusionRadiusY, ExclusionRadiusXZ);
            for (var x = corner1.X; x <= corner2.X; x++)
                for (var y = corner1.Y; y <= corner2.Y; y++)
                    for (var z = corner1.Z; z <= corner2.Z; z++)
                        propaguleBlacklist.Add(new BlockPos(x, y, z));
            context.SetBlock(placementPos, BlockProvider.GetState(context.Level, random, placementPos));
        }
    }

    //HasRequiredEmptyBlocks requires several consecutive air blocks along the placement direction, maps to vanilla hasRequiredEmptyBlocks
    private bool HasRequiredEmptyBlocks(Context context, BlockPos leafPos, PrimDirection direction)
    {
        for (var i = 1; i <= RequiredEmptyBlocks; i++)
            if (!context.IsAir(leafPos.Offset(direction.StepX * i, direction.StepY * i, direction.StepZ * i)))
                return false;
        return true;
    }
}

//PlaceOnGroundDecorator scatters decoration on the ground, used by leaf piles, maps to vanilla PlaceOnGroundDecorator
public sealed class PlaceOnGroundDecorator : TreeDecorator
{
    public static readonly MapCodec<PlaceOnGroundDecorator> Codec =
        RecordCodecBuilder.Of4<PlaceOnGroundDecorator, int, int, int, BlockStateProvider>(
            Codecs.Int.OptionalFieldOf("tries", 128).ForGetter<PlaceOnGroundDecorator, int>(d => d.Tries),
            Codecs.Int.OptionalFieldOf("radius", 2).ForGetter<PlaceOnGroundDecorator, int>(d => d.Radius),
            Codecs.Int.OptionalFieldOf("height", 1).ForGetter<PlaceOnGroundDecorator, int>(d => d.Height),
            BlockStateProvider.Codec.FieldOf("block_state_provider")
                .ForGetter<PlaceOnGroundDecorator, BlockStateProvider>(d => d.BlockStateProvider),
            (tries, radius, height, blockStateProvider) =>
                new PlaceOnGroundDecorator(tries, radius, height, blockStateProvider));

    public int Tries { get; }
    public int Radius { get; }
    public int Height { get; }
    public BlockStateProvider BlockStateProvider { get; }

    public PlaceOnGroundDecorator(int tries, int radius, int height, BlockStateProvider blockStateProvider)
    {
        Tries = tries;
        Radius = radius;
        Height = height;
        BlockStateProvider = blockStateProvider;
    }

    public override TreeDecoratorType Type => TreeDecoratorTypes.PlaceOnGround;

    public override void Place(Context context)
    {
        var blockPositions = TreeFeature.GetLowestTrunkOrRootOfTree(context);
        if (blockPositions.Count == 0) return;
        var origin = blockPositions[0];
        var minY = origin.Y;
        var minX = origin.X;
        var maxX = origin.X;
        var minZ = origin.Z;
        var maxZ = origin.Z;
        foreach (var position in blockPositions)
        {
            if (position.Y != minY) continue;
            minX = Math.Min(minX, position.X);
            maxX = Math.Max(maxX, position.X);
            minZ = Math.Min(minZ, position.Z);
            maxZ = Math.Max(maxZ, position.Z);
        }
        var random = context.Random;
        var boxMinX = minX - Radius;
        var boxMaxX = maxX + Radius;
        var boxMinY = minY - Height;
        var boxMaxY = minY + Height;
        var boxMinZ = minZ - Radius;
        var boxMaxZ = maxZ + Radius;
        for (var i = 0; i < Tries; i++)
        {
            var x = random.NextIntBetweenInclusive(boxMinX, boxMaxX);
            var y = random.NextIntBetweenInclusive(boxMinY, boxMaxY);
            var z = random.NextIntBetweenInclusive(boxMinZ, boxMaxZ);
            AttemptToPlaceBlockAbove(context, new BlockPos(x, y, z));
        }
    }

    //AttemptToPlaceBlockAbove place only when above is air or vines and the support is solid, maps to vanilla attemptToPlaceBlockAbove
    private void AttemptToPlaceBlockAbove(Context context, BlockPos pos)
    {
        var abovePos = pos.Offset(0, 1, 0);
        var aboveState = context.Level.GetBlockState(abovePos.X, abovePos.Y, abovePos.Z);
        if (!(aboveState.Owner.IsAir || ReferenceEquals(aboveState.Owner, TreeUtil.Vine))) return;
        if (!context.CheckBlock(pos, state => state.Owner.SolidRender(state))) return;
        if (context.Level.GetHeight(Heightmap.Types.MotionBlockingNoLeaves, pos.X, pos.Z) > abovePos.Y) return;
        context.SetBlock(abovePos, BlockStateProvider.GetState(context.Level, context.Random, abovePos));
    }
}

//PaleMossDecorator pale moss decorator, maps to vanilla PaleMossDecorator
//Vanilla places the pale_moss_patch configured feature when the ground roll hits; here it is looked up by registry name and placed
public sealed class PaleMossDecorator : TreeDecorator
{
    public static readonly MapCodec<PaleMossDecorator> Codec =
        RecordCodecBuilder.Of3<PaleMossDecorator, float, float, float>(
            Codecs.Float.FieldOf("leaves_probability")
                .ForGetter<PaleMossDecorator, float>(d => d.LeavesProbability),
            Codecs.Float.FieldOf("trunk_probability")
                .ForGetter<PaleMossDecorator, float>(d => d.TrunkProbability),
            Codecs.Float.FieldOf("ground_probability")
                .ForGetter<PaleMossDecorator, float>(d => d.GroundProbability),
            (leavesProbability, trunkProbability, groundProbability) =>
                new PaleMossDecorator(leavesProbability, trunkProbability, groundProbability));

    public float LeavesProbability { get; }
    public float TrunkProbability { get; }
    public float GroundProbability { get; }

    public PaleMossDecorator(float leavesProbability, float trunkProbability, float groundProbability)
    {
        LeavesProbability = leavesProbability;
        TrunkProbability = trunkProbability;
        GroundProbability = groundProbability;
    }

    public override TreeDecoratorType Type => TreeDecoratorTypes.PaleMoss;

    public override void Place(Context context)
    {
        var random = context.Random;
        var logs = TreeUtil.ShuffledCopy(context.Logs, random);
        if (logs.Count == 0) return;
        var origin = TreeUtil.MinByY(logs);
        if (random.NextFloat() < GroundProbability && context.Generator is { } generator
            && BuiltInRegistries.CONFIGURED_FEATURE.GetValue(
                Identifier.WithDefaultNamespace("pale_moss_patch"))
                is NetCraft.Game.World.Level.LevelGen.Features.ConfiguredFeature mossPatch)
            mossPatch.Place(context.Level, generator, random, origin.Offset(0, 1, 0));

        foreach (var pos in context.Logs)
        {
            if (!(random.NextFloat() < TrunkProbability)) continue;
            var down = pos.Offset(0, -1, 0);
            if (context.IsAir(down)) AddMossHanger(down, context);
        }
        foreach (var pos in context.Leaves)
        {
            if (!(random.NextFloat() < LeavesProbability)) continue;
            var down = pos.Offset(0, -1, 0);
            if (context.IsAir(down)) AddMossHanger(down, context);
        }
    }

    //AddMossHanger hang moss downward from the position, marking the tip at the end, maps to vanilla addMossHanger
    private static void AddMossHanger(BlockPos pos, Context context)
    {
        while (context.IsAir(pos.Offset(0, -1, 0)) && context.Random.NextFloat() >= 0.5d)
        {
            context.SetBlock(pos, TreeUtil.PaleHangingMoss.DefaultBlockState
                .SetValue(BlockStateProperties.Tip, false));
            pos = pos.Offset(0, -1, 0);
        }
        context.SetBlock(pos, TreeUtil.PaleHangingMoss.DefaultBlockState
            .SetValue(BlockStateProperties.Tip, true));
    }
}

//CreakingHeartDecorator creaking heart decorator, maps to vanilla CreakingHeartDecorator
//Vanilla hands off to the block entity after placing; block entities are not wired up here, so only the block itself is placed
public sealed class CreakingHeartDecorator : TreeDecorator
{
    public static readonly MapCodec<CreakingHeartDecorator> Codec =
        new SingleFieldMapCodec<CreakingHeartDecorator, float>(
            Codecs.Float.FieldOf("probability"),
            probability => new CreakingHeartDecorator(probability),
            decorator => decorator.Probability);

    public float Probability { get; }

    public CreakingHeartDecorator(float probability) => Probability = probability;

    public override TreeDecoratorType Type => TreeDecoratorTypes.CreakingHeart;

    public override void Place(Context context)
    {
        var random = context.Random;
        var logs = context.Logs;
        if (logs.Count == 0 || random.NextFloat() >= Probability) return;
        var heartPlacements = new List<BlockPos>(logs);
        TreeUtil.Shuffle(heartPlacements, random);
        foreach (var candidate in heartPlacements)
        {
            var surroundedByLogs = true;
            foreach (var dir in PrimDirection.Values)
            {
                if (context.CheckBlock(candidate.Offset(dir), state => TreeUtil.IsBlockTagged(state,
                        TreeUtil.LogsTag)))
                    continue;
                surroundedByLogs = false;
                break;
            }
            if (!surroundedByLogs) continue;
            context.SetBlock(candidate, TreeUtil.CreakingHeart.DefaultBlockState
                .SetValue(TreeDecoratorProperties.CreakingHeartStateProperty,
                    NetCraft.Registry.Enums.CreakingHeartState.dormant)
                .SetValue(TreeDecoratorProperties.Natural, true));
            return;
        }
    }
}
