using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using PrimDirection = NetCraft.Primitives.Direction;
using RegBlock = NetCraft.Registry.Block;
//A same-named field in the config would shadow the type name, so the codec references the type through an alias
using TrunkPlacerRef = NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees.TrunkPlacer;
using FoliagePlacerRef = NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees.FoliagePlacer;
using RootPlacerRef = NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees.RootPlacer;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees;

//TreeUtil shared tree utilities, gathering helpers scattered across vanilla TreeFeature/Util/Direction into one place
//Tag constants and block references are resolved lazily; treat tag matching as no match while data is not loaded
internal static class TreeUtil
{
    //Horizontal the four horizontal directions, in the NORTH EAST SOUTH WEST order of vanilla Direction.Plane.HORIZONTAL
    //Random facing and decorator traversal both depend on this order; changing it yields different trees for the same seed
    public static readonly PrimDirection[] Horizontal =
    {
        PrimDirection.North, PrimDirection.East, PrimDirection.South, PrimDirection.West,
    };

    //Pick one horizontal direction at random, maps to vanilla Direction.Plane.HORIZONTAL.getRandomDirection
    public static PrimDirection RandomHorizontalDirection(RandomSource random)
        => Horizontal[random.NextInt(Horizontal.Length)];

    //DistManhattan sum of absolute differences on the three axes, maps to vanilla BlockPos.distManhattan
    public static int DistManhattan(BlockPos a, BlockPos b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    //Shuffle in-place shuffle, maps to vanilla Util.shuffle, Fisher-Yates from the end
    public static void Shuffle<T>(IList<T> list, RandomSource random)
    {
        for (var i = list.Count; i > 1; i--)
        {
            var j = random.NextInt(i);
            (list[i - 1], list[j]) = (list[j], list[i - 1]);
        }
    }

    //ShuffledCopy copy then shuffle, maps to vanilla Util.shuffledCopy
    public static List<T> ShuffledCopy<T>(IEnumerable<T> source, RandomSource random)
    {
        var copy = source.ToList();
        Shuffle(copy, random);
        return copy;
    }

    //GetRandom pick one element uniformly at random, maps to vanilla Util.getRandom
    public static T GetRandom<T>(IReadOnlyList<T> list, RandomSource random)
        => list[random.NextInt(list.Count)];

    //AsEnumAxis convert a game axis to the property enum axis; FancyTrunkPlacer and CherryTrunkPlacer use it to write the axis property
    public static Axis ToEnumAxis(PrimDirection.Axis axis) => axis switch
    {
        PrimDirection.Axis.X => Axis.x,
        PrimDirection.Axis.Z => Axis.z,
        _ => Axis.y,
    };

    //Air tag, reusing the same one already defined in the block predicates
    public static readonly TagKey<RegBlock> AirTag = BlockPredicate.AirTag;

    //Leaves tag, reused by the LeavesBlock distance check and other tree-related checks
    public static readonly TagKey<RegBlock> LeavesTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("leaves"));

    //Replaceable by trees tag, maps to vanilla BlockTags.REPLACEABLE_BY_TREES
    public static readonly TagKey<RegBlock> ReplaceableByTreesTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("replaceable_by_trees"));

    //Logs tag, maps to vanilla BlockTags.LOGS
    public static readonly TagKey<RegBlock> LogsTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("logs"));

    //Cannot replace below tree trunk tag, maps to vanilla BlockTags.CANNOT_REPLACE_BELOW_TREE_TRUNK
    public static readonly TagKey<RegBlock> CannotReplaceBelowTreeTrunkTag =
        TagKey<RegBlock>.Create(Registries.BLOCK,
            Identifier.WithDefaultNamespace("cannot_replace_below_tree_trunk"));

    //Prevents nearby leaf decay tag, maps to vanilla BlockTags.PREVENTS_NEARBY_LEAF_DECAY
    public static readonly TagKey<RegBlock> PreventsNearbyLeafDecayTag =
        TagKey<RegBlock>.Create(Registries.BLOCK,
            Identifier.WithDefaultNamespace("prevents_nearby_leaf_decay"));

    //Leaf distance-to-trunk property, range 1-7, maps to vanilla BlockStateProperties.DISTANCE
    public static readonly IntegerProperty Distance = new("distance", 1, 7);

    //Leaf persistent property, maps to vanilla BlockStateProperties.PERSISTENT
    public static readonly BooleanProperty Persistent = new("persistent");

    //Waterlogged property, maps to vanilla BlockStateProperties.WATERLOGGED
    public static readonly BooleanProperty Waterlogged = new("waterlogged");

    //Vine block resolved lazily once, used when placing trunk and leaf vines
    private static RegBlock? _vine;
    private static RegBlock? _cocoa;
    private static RegBlock? _beeNest;
    private static RegBlock? _paleHangingMoss;
    private static RegBlock? _creakingHeart;

    public static RegBlock Vine => _vine ??= Lookup("vine");

    public static RegBlock Cocoa => _cocoa ??= Lookup("cocoa");

    public static RegBlock BeeNest => _beeNest ??= Lookup("bee_nest");

    public static RegBlock PaleHangingMoss => _paleHangingMoss ??= Lookup("pale_hanging_moss");

    public static RegBlock CreakingHeart => _creakingHeart ??= Lookup("creaking_heart");

    //IsBlockTagged whether the block state is in the tag; treat an unbound tag as no match, maps to vanilla state.is(TagKey)
    public static bool IsBlockTagged(BlockState state, TagKey<RegBlock> tag)
    {
        var set = BuiltInRegistries.BLOCK.Get(tag);
        return set is { IsBound: true } && set.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner));
    }

    //IsVine whether the position is a vine, maps to vanilla TreeFeature.isVine
    public static bool IsVine(WorldGenRegion level, BlockPos pos)
        => ReferenceEquals(level.GetBlockState(pos.X, pos.Y, pos.Z).Owner, Vine);

    //IsAirOrLeaves whether the position is air or leaves, maps to vanilla TreeFeature.isAirOrLeaves
    public static bool IsAirOrLeaves(WorldGenRegion level, BlockPos pos)
    {
        var state = level.GetBlockState(pos.X, pos.Y, pos.Z);
        return state.Owner.IsAir || IsBlockTagged(state, LeavesTag);
    }

    //ValidTreePos whether the position can be taken by a tree, maps to vanilla TreeFeature.validTreePos
    public static bool ValidTreePos(WorldGenRegion level, BlockPos pos)
    {
        var state = level.GetBlockState(pos.X, pos.Y, pos.Z);
        return state.Owner.IsAir || IsBlockTagged(state, ReplaceableByTreesTag);
    }

    //IsLogs whether the position is a log, maps to vanilla state.is(BlockTags.LOGS)
    public static bool IsLogs(WorldGenRegion level, BlockPos pos)
        => IsBlockTagged(level.GetBlockState(pos.X, pos.Y, pos.Z), LogsTag);

    //IsWaterAt whether the position holds a water source, maps to vanilla level.isFluidAtPosition(pos, s -> s.is(FluidTags.WATER))
    //NetCraft fluid states only track "is fluid" and the fallback block; here water is decided by the fallback block's registry name
    public static bool IsWaterAt(WorldGenRegion level, BlockPos pos)
    {
        var fluid = level.GetBlockState(pos.X, pos.Y, pos.Z).FluidState;
        if (fluid.IsEmpty) return false;
        return fluid.CreateLegacyBlock().Owner.Id.Path == "water";
    }

    //OptionalDistanceAt leaf distance carried by the block state, maps to vanilla LeavesBlock.getOptionalDistanceAt
    public static int? OptionalDistanceAt(BlockState state)
    {
        if (IsBlockTagged(state, PreventsNearbyLeafDecayTag)) return 0;
        return state.HasProperty(Distance) ? state.GetValue(Distance) : null;
    }

    //MinByY take the item with the smallest Y, first one wins on ties, maps to vanilla Collections.min(comparingInt(getY))
    public static BlockPos MinByY(IReadOnlyList<BlockPos> positions)
    {
        var best = positions[0];
        foreach (var pos in positions)
            if (pos.Y < best.Y) best = pos;
        return best;
    }

    //Lookup fetch a registered block by registry name; falls back to air when unregistered
    private static RegBlock Lookup(string path)
        => BuiltInRegistries.BLOCK.GetValue(Identifier.WithDefaultNamespace(path))
           ?? BuiltInRegistries.BLOCK.GetValue(Identifier.WithDefaultNamespace("air"))!;
}

//TreeConfiguration tree configuration, maps to vanilla TreeConfiguration
public sealed class TreeConfiguration : FeatureConfiguration
{
    public static readonly Codec<TreeConfiguration> Codec =
        RecordCodecBuilder.Of9<TreeConfiguration, BlockStateProvider, TrunkPlacer, BlockStateProvider,
            FoliagePlacer, Optional<RootPlacer>, FeatureSize, IReadOnlyList<TreeDecorator>, bool,
            BlockStateProvider>(
            BlockStateProvider.Codec.FieldOf("trunk_provider")
                .ForGetter<TreeConfiguration, BlockStateProvider>(c => c.TrunkProvider),
            TrunkPlacerRef.Codec.FieldOf("trunk_placer")
                .ForGetter<TreeConfiguration, TrunkPlacer>(c => c.TrunkPlacer),
            BlockStateProvider.Codec.FieldOf("foliage_provider")
                .ForGetter<TreeConfiguration, BlockStateProvider>(c => c.FoliageProvider),
            FoliagePlacerRef.Codec.FieldOf("foliage_placer")
                .ForGetter<TreeConfiguration, FoliagePlacer>(c => c.FoliagePlacer),
            RootPlacerRef.Codec.OptionalFieldOf("root_placer")
                .ForGetter<TreeConfiguration, Optional<RootPlacer>>(c => c.RootPlacer),
            FeatureSize.Codec.FieldOf("minimum_size")
                .ForGetter<TreeConfiguration, FeatureSize>(c => c.MinimumSize),
            TreeDecorator.Codec.ListOf().FieldOf("decorators")
                .ForGetter<TreeConfiguration, IReadOnlyList<TreeDecorator>>(c => c.Decorators),
            Codecs.Bool.OptionalFieldOf("ignore_vines", false)
                .ForGetter<TreeConfiguration, bool>(c => c.IgnoreVines),
            BlockStateProvider.Codec.FieldOf("below_trunk_provider")
                .ForGetter<TreeConfiguration, BlockStateProvider>(c => c.BelowTrunkProvider),
            (trunkProvider, trunkPlacer, foliageProvider, foliagePlacer, rootPlacer, minimumSize,
                    decorators, ignoreVines, belowTrunkProvider) =>
                new TreeConfiguration(trunkProvider, trunkPlacer, foliageProvider, foliagePlacer, rootPlacer,
                    minimumSize, decorators, ignoreVines, belowTrunkProvider));

    public BlockStateProvider TrunkProvider { get; }
    public TrunkPlacer TrunkPlacer { get; }
    public BlockStateProvider FoliageProvider { get; }
    public FoliagePlacer FoliagePlacer { get; }
    public Optional<RootPlacer> RootPlacer { get; }
    public FeatureSize MinimumSize { get; }
    public IReadOnlyList<TreeDecorator> Decorators { get; }
    public bool IgnoreVines { get; }
    public BlockStateProvider BelowTrunkProvider { get; }

    public TreeConfiguration(BlockStateProvider trunkProvider, TrunkPlacer trunkPlacer,
        BlockStateProvider foliageProvider, FoliagePlacer foliagePlacer, Optional<RootPlacer> rootPlacer,
        FeatureSize minimumSize, IReadOnlyList<TreeDecorator> decorators, bool ignoreVines,
        BlockStateProvider belowTrunkProvider)
    {
        TrunkProvider = trunkProvider;
        TrunkPlacer = trunkPlacer;
        FoliageProvider = foliageProvider;
        FoliagePlacer = foliagePlacer;
        RootPlacer = rootPlacer;
        MinimumSize = minimumSize;
        Decorators = decorators;
        IgnoreVines = ignoreVines;
        BelowTrunkProvider = belowTrunkProvider;
    }
}

//TreeFeature tree feature, maps to vanilla TreeFeature, registered as tree
public sealed class TreeFeature : Feature<TreeConfiguration>
{
    private const string FeatureId = "tree";

    public static readonly TreeFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new TreeFeature());

    private TreeFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), TreeConfiguration.Codec) { }

    //Place place the whole tree, maps to vanilla place
    protected override bool Place(TreeConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var random = context.Random;
        var origin = context.Origin;
        var rootPositions = new HashSet<BlockPos>();
        var trunks = new HashSet<BlockPos>();
        var foliage = new HashSet<BlockPos>();
        var decorations = new HashSet<BlockPos>();

        void RootSetter(BlockPos pos, BlockState state)
        {
            rootPositions.Add(pos);
            level.SetBlockState(pos.X, pos.Y, pos.Z, state);
        }

        void TrunkSetter(BlockPos pos, BlockState state)
        {
            trunks.Add(pos);
            level.SetBlockState(pos.X, pos.Y, pos.Z, state);
        }

        void FoliageSetterImpl(BlockPos pos, BlockState state)
        {
            foliage.Add(pos);
            level.SetBlockState(pos.X, pos.Y, pos.Z, state);
        }

        void DecorationSetter(BlockPos pos, BlockState state)
        {
            decorations.Add(pos);
            level.SetBlockState(pos.X, pos.Y, pos.Z, state);
        }

        var foliageSetter = new FoliagePlacer.FoliageSetter(FoliageSetterImpl, foliage.Contains);
        if (!DoPlace(level, random, origin, RootSetter, TrunkSetter, foliageSetter, config)) return false;
        if (trunks.Count == 0 && foliage.Count == 0) return false;

        if (config.Decorators.Count > 0)
        {
            var decoratorContext = new TreeDecorator.Context(level, DecorationSetter, random, context.ChunkGenerator,
                trunks, foliage, rootPositions);
            foreach (var decorator in config.Decorators)
                decorator.Place(decoratorContext);
        }

        var all = new List<BlockPos>(rootPositions.Count + trunks.Count + foliage.Count + decorations.Count);
        all.AddRange(rootPositions);
        all.AddRange(trunks);
        all.AddRange(foliage);
        all.AddRange(decorations);
        if (all.Count == 0) return false;

        UpdateLeaves(level, all, trunks, decorations, rootPositions);
        return true;
    }

    //DoPlace grow one tree from the config, maps to vanilla doPlace
    private static bool DoPlace(WorldGenRegion level, RandomSource random, BlockPos origin,
        Action<BlockPos, BlockState> rootSetter, Action<BlockPos, BlockState> trunkSetter,
        FoliagePlacer.FoliageSetter foliageSetter, TreeConfiguration config)
    {
        var treeHeight = config.TrunkPlacer.GetTreeHeight(random);
        var foliageHeight = config.FoliagePlacer.FoliageHeight(random, treeHeight, config);
        var trunkHeight = treeHeight - foliageHeight;
        var leafRadius = config.FoliagePlacer.FoliageRadius(random, trunkHeight);
        var trunkOrigin = config.RootPlacer.IsPresent
            ? config.RootPlacer.Get().GetTrunkOrigin(origin, random)
            : origin;
        var minY = Math.Min(origin.Y, trunkOrigin.Y);
        var maxY = Math.Max(origin.Y, trunkOrigin.Y) + treeHeight + 1;
        var minBuildHeight = level.MinSectionY * 16;
        var maxBuildHeight = (level.MaxSectionY + 1) * 16;
        if (minY < minBuildHeight + 1 || maxY > maxBuildHeight) return false;

        var minClippedHeight = config.MinimumSize.MinClippedHeight;
        var clippedTreeHeight = GetMaxFreeTreeHeight(level, treeHeight, trunkOrigin, config);
        if (clippedTreeHeight < treeHeight
            && (minClippedHeight is null || clippedTreeHeight < minClippedHeight.Value))
            return false;

        if (config.RootPlacer.IsPresent
            && !config.RootPlacer.Get().PlaceRoots(level, rootSetter, random, origin, trunkOrigin, config))
            return false;

        var foliageAttachments = config.TrunkPlacer.PlaceTrunk(level, trunkSetter, random, clippedTreeHeight,
            trunkOrigin, config);
        foreach (var attachment in foliageAttachments)
            config.FoliagePlacer.CreateFoliage(level, foliageSetter, random, config, clippedTreeHeight,
                attachment, foliageHeight, leafRadius);
        return true;
    }

    //GetMaxFreeTreeHeight max height the trunk can reach; hitting an obstacle cuts two blocks, maps to vanilla getMaxFreeTreeHeight
    private static int GetMaxFreeTreeHeight(WorldGenRegion level, int maxTreeHeight, BlockPos treePos,
        TreeConfiguration config)
    {
        for (var y = 0; y <= maxTreeHeight + 1; y++)
        {
            var r = config.MinimumSize.GetSizeAtHeight(maxTreeHeight, y);
            for (var x = -r; x <= r; x++)
            {
                for (var z = -r; z <= r; z++)
                {
                    var pos = treePos.Offset(x, y, z);
                    if (!config.TrunkPlacer.IsFree(level, pos)
                        || (!config.IgnoreVines && TreeUtil.IsVine(level, pos)))
                        return y - 2;
                }
            }
        }
        return maxTreeHeight;
    }

    //UpdateLeaves spread outward from the trunk and recompute the leaf distance property, maps to vanilla updateLeaves
    //Vanilla also calls StructureTemplate.updateShapeAtEdge to refresh edge lighting and shape; there is no lighting engine here, so it is skipped
    private static void UpdateLeaves(WorldGenRegion level, IReadOnlyList<BlockPos> all, HashSet<BlockPos> logs,
        HashSet<BlockPos> decorations, HashSet<BlockPos> rootPositions)
    {
        var minX = all[0].X;
        var minY = all[0].Y;
        var minZ = all[0].Z;
        var maxX = minX;
        var maxY = minY;
        var maxZ = minZ;
        foreach (var pos in all)
        {
            minX = Math.Min(minX, pos.X);
            minY = Math.Min(minY, pos.Y);
            minZ = Math.Min(minZ, pos.Z);
            maxX = Math.Max(maxX, pos.X);
            maxY = Math.Max(maxY, pos.Y);
            maxZ = Math.Max(maxZ, pos.Z);
        }

        var shape = new HashSet<(int, int, int)>();
        foreach (var pos in decorations) FillShape(shape, pos, minX, minY, minZ, maxX, maxY, maxZ);
        foreach (var pos in rootPositions) FillShape(shape, pos, minX, minY, minZ, maxX, maxY, maxZ);

        var toCheck = new List<HashSet<BlockPos>>(7);
        for (var i = 0; i < 7; i++) toCheck.Add(new HashSet<BlockPos>());
        var smallestDistance = 0;
        toCheck[0].UnionWith(logs);

        while (true)
        {
            if (smallestDistance < 7 && toCheck[smallestDistance].Count == 0)
            {
                smallestDistance++;
                continue;
            }
            if (smallestDistance >= 7) return;

            var bucket = toCheck[smallestDistance];
            var pos = default(BlockPos);
            var found = false;
            foreach (var candidate in bucket)
            {
                pos = candidate;
                found = true;
                break;
            }
            if (!found) continue;
            bucket.Remove(pos);

            if (!Inside(pos, minX, minY, minZ, maxX, maxY, maxZ)) continue;

            if (smallestDistance != 0)
            {
                var state = level.GetBlockState(pos.X, pos.Y, pos.Z);
                level.SetBlockState(pos.X, pos.Y, pos.Z, state.SetValue(TreeUtil.Distance, smallestDistance));
            }
            FillShape(shape, pos, minX, minY, minZ, maxX, maxY, maxZ);

            foreach (var direction in PrimDirection.Values)
            {
                var neighbor = pos.Offset(direction);
                if (!Inside(neighbor, minX, minY, minZ, maxX, maxY, maxZ)) continue;
                if (shape.Contains((neighbor.X - minX, neighbor.Y - minY, neighbor.Z - minZ))) continue;
                var currentState = level.GetBlockState(neighbor.X, neighbor.Y, neighbor.Z);
                var distance = TreeUtil.OptionalDistanceAt(currentState);
                if (distance is null) continue;
                var newDistance = Math.Min(distance.Value, smallestDistance + 1);
                if (newDistance >= 7) continue;
                toCheck[newDistance].Add(neighbor);
                smallestDistance = Math.Min(smallestDistance, newDistance);
            }
        }
    }

    private static void FillShape(HashSet<(int, int, int)> shape, BlockPos pos, int minX, int minY, int minZ,
        int maxX, int maxY, int maxZ)
    {
        if (!Inside(pos, minX, minY, minZ, maxX, maxY, maxZ)) return;
        shape.Add((pos.X - minX, pos.Y - minY, pos.Z - minZ));
    }

    private static bool Inside(BlockPos pos, int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        => pos.X >= minX && pos.X <= maxX && pos.Y >= minY && pos.Y <= maxY && pos.Z >= minZ && pos.Z <= maxZ;

    //GetLowestTrunkOrRootOfTree get the trunk or root positions at the tree's lowest layer, maps to vanilla getLowestTrunkOrRootOfTree
    public static List<BlockPos> GetLowestTrunkOrRootOfTree(TreeDecorator.Context context)
    {
        var result = new List<BlockPos>();
        var roots = context.Roots;
        var logs = context.Logs;
        if (roots.Count == 0)
        {
            result.AddRange(logs);
        }
        else if (logs.Count > 0 && roots[0].Y == logs[0].Y)
        {
            result.AddRange(logs);
            result.AddRange(roots);
        }
        else
        {
            result.AddRange(roots);
        }
        return result;
    }
}
