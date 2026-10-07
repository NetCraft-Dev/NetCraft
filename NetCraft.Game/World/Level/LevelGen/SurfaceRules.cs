using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util;
using NetCraft.Util.Random;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Game.World.Level.LevelGen;

//SurfaceRules is the surface rule system, maps to vanilla net.minecraft.world.level.levelgen.SurfaceRules
//Provides two kinds of serializable objects: condition source ConditionSource and rule source RuleSource
//MATERIAL_CONDITION/MATERIAL_RULE use a weakly typed MapCodec<object> to align with the cross-layer plan
//Evaluation is implemented "per block on demand"; it does not copy vanilla's lazy Condition object layer, results are equivalent
public static class SurfaceRules
{
    //Context is the rule evaluation context; SurfaceSystem calls updateXZ once per column and updateY once per block
    //Field semantics align with vanilla SurfaceRules.Context; each Condition's evaluation formula reads these fields directly
    public sealed class Context
    {
        //HowFarBelowPreliminarySurface is the depth below the preliminary surface still involved in building, maps to the vanilla constant of the same name
        private const int HowFarBelowPreliminarySurface = 8;
        //SurfaceCellSize samples the preliminary surface on a 16-block grid with corner interpolation, maps to vanilla SURFACE_CELL_SIZE
        private const int SurfaceCellSize = 16;

        //DefaultMinY/DefaultHeight is the overworld dimension height range, used as a fallback for VerticalAnchor resolution
        public const int DefaultMinY = -64;
        public const int DefaultHeight = 384;

        private readonly SurfaceSystem _system;
        private readonly ChunkAccess? _chunk;
        private readonly NoiseChunk? _noiseChunk;
        private readonly Func<int, int, int, Biome>? _biomeGetter;
        private readonly Dictionary<NoiseHolder, NoiseSampler> _samplers = new();

        private double _surfaceSecondary;
        private bool _hasSurfaceSecondary;
        private int _minSurfaceLevel;
        private bool _hasMinSurfaceLevel;
        private Biome? _biome;

        public int MinY { get; }
        public int Height { get; }
        public int BlockX { get; private set; }
        public int BlockY { get; private set; }
        public int BlockZ { get; private set; }
        //SurfaceDepth is this column's surface depth noise, the result of vanilla getSurfaceDepth
        public int SurfaceDepth { get; private set; }
        //WaterHeight is this column's most recent water surface height, int.MinValue when there is no fluid, maps to vanilla waterHeight
        public int WaterHeight { get; private set; } = int.MinValue;
        public int StoneDepthBelow { get; private set; }
        public int StoneDepthAbove { get; private set; }

        public Context(SurfaceSystem system, ChunkAccess? chunk, NoiseChunk? noiseChunk,
            Func<int, int, int, Biome>? biomeGetter, int minY, int height)
        {
            _system = system;
            _chunk = chunk;
            _noiseChunk = noiseChunk;
            _biomeGetter = biomeGetter;
            MinY = minY;
            Height = height;
        }

        //UpdateXZ is called when switching columns; recomputes surfaceDepth and invalidates per-column caches
        public void UpdateXZ(int blockX, int blockZ)
        {
            BlockX = blockX;
            BlockZ = blockZ;
            SurfaceDepth = _system.GetSurfaceDepth(blockX, blockZ);
            _hasSurfaceSecondary = false;
            _hasMinSurfaceLevel = false;
        }

        //UpdateY is called when switching blocks; the biome varies with y and is invalidated too
        public void UpdateY(int stoneDepthAbove, int stoneDepthBelow, int waterHeight, int blockY)
        {
            BlockY = blockY;
            WaterHeight = waterHeight;
            StoneDepthBelow = stoneDepthBelow;
            StoneDepthAbove = stoneDepthAbove;
            _biome = null;
        }

        //GetSurfaceSecondary lazily computes the secondary surface noise per column, maps to vanilla getSurfaceSecondary
        public double GetSurfaceSecondary()
        {
            if (!_hasSurfaceSecondary)
            {
                _surfaceSecondary = _system.GetSurfaceSecondary(BlockX, BlockZ);
                _hasSurfaceSecondary = true;
            }
            return _surfaceSecondary;
        }

        //GetMinSurfaceLevel is the lower bound of the preliminary surface: 16-grid corner interpolation minus surfaceDepth and an 8-block margin
        //Maps to vanilla getMinSurfaceLevel; degrades to sea level when there is no NoiseChunk, only the test path hits this
        public int GetMinSurfaceLevel()
        {
            if (_hasMinSurfaceLevel) return _minSurfaceLevel;
            _hasMinSurfaceLevel = true;
            if (_noiseChunk is null)
            {
                _minSurfaceLevel = _system.SeaLevel;
                return _minSurfaceLevel;
            }
            var cornerX = BlockX >> 4;
            var cornerZ = BlockZ >> 4;
            var c00 = _noiseChunk.PreliminarySurfaceLevel(cornerX * SurfaceCellSize, cornerZ * SurfaceCellSize);
            var c10 = _noiseChunk.PreliminarySurfaceLevel((cornerX + 1) * SurfaceCellSize, cornerZ * SurfaceCellSize);
            var c01 = _noiseChunk.PreliminarySurfaceLevel(cornerX * SurfaceCellSize, (cornerZ + 1) * SurfaceCellSize);
            var c11 = _noiseChunk.PreliminarySurfaceLevel((cornerX + 1) * SurfaceCellSize, (cornerZ + 1) * SurfaceCellSize);
            var fx = (BlockX & 15) / (float)SurfaceCellSize;
            var fz = (BlockZ & 15) / (float)SurfaceCellSize;
            var preliminary = Mth.Floor(Mth.Lerp2(fx, fz, c00, c10, c01, c11));
            _minSurfaceLevel = preliminary + SurfaceDepth - HowFarBelowPreliminarySurface;
            return _minSurfaceLevel;
        }

        //GetBiome is the biome at the current block, lazily fetched by y, maps to vanilla getBiome
        public Biome GetBiome()
        {
            if (_biome is null)
                _biome = _biomeGetter?.Invoke(BlockX, BlockY, BlockZ) ?? Biome.Plains;
            return _biome;
        }

        public int GetSeaLevel() => _system.SeaLevel;

        //GetBand is the badlands band material, maps to surfaceSystem.getBand referenced by vanilla's bandlands rule
        public BlockState GetBand() => _system.GetBand(BlockX, BlockY, BlockZ);

        //SampleNoise gets the sampler for a noise; 2d caches by XZ only, 3d caches by XY Z, maps to vanilla getNoiseSampler
        public double SampleNoise(NoiseHolder noise, bool is3d)
        {
            if (!_samplers.TryGetValue(noise, out var sampler))
            {
                sampler = new NoiseSampler(this, noise, is3d);
                _samplers[noise] = sampler;
            }
            return sampler.Value;
        }

        //GetRandomFactory gets the named positional random factory used by vertical_gradient to roll a probability
        public PositionalRandomFactory? GetRandomFactory(Identifier name)
            => _system.RandomState?.GetOrCreateRandomFactory(name);

        //GetHeight gets this column's WORLD_SURFACE_WG height for the steep condition to compare neighbor slopes
        public int GetHeight(int localX, int localZ)
            => _chunk?.GetHeight(HeightmapRegistry.Types.WorldSurfaceWg, localX, localZ) ?? int.MinValue;

        //NoiseSampler caches a single noise sample; re-evaluation at the same coordinates computes only once
        private sealed class NoiseSampler
        {
            private readonly Context _owner;
            private readonly NoiseHolder _noise;
            private readonly bool _is3d;
            private int _x = int.MinValue;
            private int _y;
            private int _z;
            private bool _has;

            public NoiseSampler(Context owner, NoiseHolder noise, bool is3d)
            {
                _owner = owner;
                _noise = noise;
                _is3d = is3d;
            }

            public double Value
            {
                get
                {
                    var y = _is3d ? _owner.BlockY : 0;
                    if (_has && _x == _owner.BlockX && _y == y && _z == _owner.BlockZ) return _cached;
                    _x = _owner.BlockX;
                    _y = y;
                    _z = _owner.BlockZ;
                    _cached = _noise.GetValue(_x, _y, _z);
                    _has = true;
                    return _cached;
                }
            }

            private double _cached;
        }
    }

    //SurfaceType is the stone top/bottom surface facing, maps to vanilla CaveSurface
    public enum SurfaceType
    {
        Floor,
        Ceiling
    }

    //ConditionSource is registered in the MATERIAL_CONDITION registry
    //Implementations decide via Context whether to enter a rule branch
    public interface ConditionSource
    {
        bool Test(Context context);
    }

    //RuleSource is registered in the MATERIAL_RULE registry
    //Returns the replaced BlockState, or null for no replacement
    public interface RuleSource
    {
        BlockState? Apply(Context context, BlockState current);
    }

    //BlockStateRule holds a fixed BlockState and replaces directly, maps to vanilla SurfaceRules.state
    public sealed class BlockStateRule : RuleSource
    {
        public BlockState State { get; }

        public BlockStateRule(BlockState state)
        {
            State = state;
        }

        public BlockState? Apply(Context context, BlockState current) => State;
    }

    //IfTrue applies the nested rule when the condition holds, maps to vanilla SurfaceRules.ifTrue
    public sealed class IfTrue : RuleSource
    {
        public ConditionSource Condition { get; }
        public RuleSource Then { get; }

        public IfTrue(ConditionSource condition, RuleSource then)
        {
            Condition = condition;
            Then = then;
        }

        public BlockState? Apply(Context context, BlockState current)
            => Condition.Test(context) ? Then.Apply(context, current) : null;
    }

    //Sequence returns on the first hit in the ordered rule list, maps to vanilla SurfaceRules.sequence
    //It is not that later rules override earlier ones; rules placed first such as bedrock must be able to block later deepslate
    public sealed class Sequence : RuleSource
    {
        //Rules is the rule array; every block walks it from the start, storing an array allows index access instead of a boxing interface enumerator
        public RuleSource[] Rules { get; }

        public Sequence(IReadOnlyList<RuleSource> rules)
        {
            Rules = rules as RuleSource[] ?? rules.ToArray();
        }

        public BlockState? Apply(Context context, BlockState current)
        {
            foreach (var rule in Rules)
            {
                var next = rule.Apply(context, current);
                if (next is not null) return next;
            }
            return null;
        }
    }

    //AbovePreliminarySurface tests whether y is above the preliminary surface lower bound, maps to vanilla above_preliminary_surface
    public sealed class AbovePreliminarySurface : ConditionSource
    {
        public bool Test(Context context) => context.BlockY >= context.GetMinSurfaceLevel();
    }

    //StoneDepth tests whether the stone depth from the surface is within the threshold, maps to vanilla SurfaceRules.stoneDepth
    public sealed class StoneDepth : ConditionSource
    {
        public int Offset { get; }
        public bool AddSurfaceDepth { get; }
        public int SecondaryDepthRange { get; }
        public SurfaceType Surface { get; }

        public StoneDepth(int offset, bool addSurfaceDepth = false, int secondaryDepthRange = 0,
            SurfaceType surface = SurfaceType.Floor)
        {
            Offset = offset;
            AddSurfaceDepth = addSurfaceDepth;
            SecondaryDepthRange = secondaryDepthRange;
            Surface = surface;
        }

        //Test uses stoneDepthBelow for ceilings and stoneDepthAbove for floors; the threshold includes 1 + offset + both surface depths
        public bool Test(Context context)
        {
            var stoneDepth = Surface == SurfaceType.Ceiling ? context.StoneDepthBelow : context.StoneDepthAbove;
            var surfaceDepth = AddSurfaceDepth ? context.SurfaceDepth : 0;
            var secondaryDepth = SecondaryDepthRange == 0
                ? 0
                : (int)Mth.Map(context.GetSurfaceSecondary(), -1.0, 1.0, 0.0, SecondaryDepthRange);
            return stoneDepth <= 1 + Offset + surfaceDepth + secondaryDepth;
        }
    }

    //VerticalGradient appears with a probability increasing along y, maps to vanilla vertical_gradient
    //Always true at or below true_at_and_below, always false at or above false_at_and_above, and positional random in between
    public sealed class VerticalGradient : ConditionSource
    {
        public Identifier RandomName { get; }
        public VerticalAnchor TrueAtAndBelow { get; }
        public VerticalAnchor FalseAtAndAbove { get; }

        public VerticalGradient(VerticalAnchor trueAtAndBelow, VerticalAnchor falseAtAndAbove,
            Identifier randomName)
        {
            TrueAtAndBelow = trueAtAndBelow;
            FalseAtAndAbove = falseAtAndAbove;
            RandomName = randomName;
        }

        //Simplified VerticalGradient constructor without a random name; the middle section uses the default positional random source
        public VerticalGradient(VerticalAnchor trueAtAndBelow, VerticalAnchor falseAtAndAbove)
            : this(trueAtAndBelow, falseAtAndAbove, Identifier.WithDefaultNamespace("default"))
        {
        }

        public bool Test(Context context)
        {
            var y = context.BlockY;
            var trueAt = TrueAtAndBelow.ResolveY(context.MinY, context.Height);
            if (y <= trueAt) return true;
            var falseAt = FalseAtAndAbove.ResolveY(context.MinY, context.Height);
            if (y >= falseAt) return false;
            var probability = Mth.Map(y, trueAt, falseAt, 1.0, 0.0);
            var factory = context.GetRandomFactory(RandomName);
            if (factory is null) return probability >= 0.5;
            return factory.At(context.BlockX, y, context.BlockZ).NextFloat() < probability;
        }
    }

    //Not is the negation condition, maps to vanilla SurfaceRules.not
    public sealed class Not : ConditionSource
    {
        public ConditionSource Inner { get; }

        public Not(ConditionSource inner)
        {
            Inner = inner;
        }

        public bool Test(Context context) => !Inner.Test(context);
    }

    //BiomeCondition tests whether the current biome matches, maps to vanilla SurfaceRules.biome
    //biome_is can be either an explicit biome list or the "#namespace:tag" form
    public sealed class BiomeCondition : ConditionSource
    {
        //Biomes lists explicit biomes, empty in tag form
        //Every block compares against it, storing an array allows index access instead of a boxing interface enumerator
        public Biome[] Biomes { get; }
        //Tag is the tag reference, written as "#minecraft:is_forest" in vanilla biome_is
        public TagKey<Biome>? Tag { get; }
        //TagRegistry is the biome registry used to resolve tags, non-null in tag form
        public Registry<Biome>? TagRegistry { get; }

        public BiomeCondition(IReadOnlyList<Biome> biomes)
        {
            Biomes = biomes as Biome[] ?? biomes.ToArray();
        }

        public BiomeCondition(TagKey<Biome> tag, Registry<Biome> registry)
        {
            Biomes = Array.Empty<Biome>();
            Tag = tag;
            TagRegistry = registry;
        }

        public bool Test(Context context)
        {
            var biome = context.GetBiome();
            if (Tag is not null)
            {
                //An unbound tag counts as no match, equivalent to vanilla's empty-tag semantics
                var set = TagRegistry?.Get(Tag);
                return set is { IsBound: true } && set.Contains(TagRegistry!.WrapAsHolder(biome));
            }
            foreach (var b in Biomes)
                if (b.Id == biome.Id) return true;
            return false;
        }
    }

    //NoiseThreshold is the noise threshold condition, maps to vanilla SurfaceRules.noiseThreshold
    public sealed class NoiseThreshold : ConditionSource
    {
        public NoiseHolder Noise { get; }
        public double MinThreshold { get; }
        public double MaxThreshold { get; }
        public bool Is3D { get; }

        public NoiseThreshold(NoiseHolder noise, double minThreshold, double maxThreshold, bool is3D = false)
        {
            Noise = noise;
            MinThreshold = minThreshold;
            MaxThreshold = maxThreshold;
            Is3D = is3D;
        }

        public bool Test(Context context)
        {
            var value = context.SampleNoise(Noise, Is3D);
            return value >= MinThreshold && value <= MaxThreshold;
        }
    }

    //Water is the water surface condition, maps to vanilla SurfaceRules.water
    //Always true when the column has no fluid; with fluid only true at or above the given offset
    public sealed class Water : ConditionSource
    {
        public int Offset { get; }
        public bool AddStoneDepth { get; }
        public int SurfaceDepthMultiplier { get; }

        public Water(int offset, bool addStoneDepth, int surfaceDepthMultiplier)
        {
            Offset = offset;
            AddStoneDepth = addStoneDepth;
            SurfaceDepthMultiplier = surfaceDepthMultiplier;
        }

        public bool Test(Context context)
        {
            if (context.WaterHeight == int.MinValue) return true;
            var y = context.BlockY + (AddStoneDepth ? context.StoneDepthAbove : 0);
            return y >= context.WaterHeight + Offset + context.SurfaceDepth * SurfaceDepthMultiplier;
        }
    }

    //YAbove is the y-above-anchor condition, maps to vanilla SurfaceRules.yAbove
    public sealed class YAbove : ConditionSource
    {
        public VerticalAnchor Anchor { get; }
        public int SurfaceDepthMultiplier { get; }
        public bool AddStoneDepth { get; }

        public YAbove(VerticalAnchor anchor, int surfaceDepthMultiplier, bool addStoneDepth)
        {
            Anchor = anchor;
            SurfaceDepthMultiplier = surfaceDepthMultiplier;
            AddStoneDepth = addStoneDepth;
        }

        public bool Test(Context context)
        {
            var y = context.BlockY + (AddStoneDepth ? context.StoneDepthAbove : 0);
            return y >= Anchor.ResolveY(context.MinY, context.Height)
                + context.SurfaceDepth * SurfaceDepthMultiplier;
        }
    }

    //Temperature is the cold condition, maps to vanilla SurfaceRules.temperature; true means cold enough to snow there
    public sealed class Temperature : ConditionSource
    {
        public bool Test(Context context)
            => BiomeTemperature.ColdEnoughToSnow(context.GetBiome(), context.BlockX, context.BlockY,
                context.BlockZ, context.GetSeaLevel());
    }

    //Steep is the steep-slope condition, maps to vanilla SurfaceRules.steep; a height difference of 4 between north/south or east/west neighbor columns counts as steep
    public sealed class Steep : ConditionSource
    {
        public bool Test(Context context)
        {
            var chunkBlockX = context.BlockX & 15;
            var chunkBlockZ = context.BlockZ & 15;
            var zNorth = Math.Max(chunkBlockZ - 1, 0);
            var zSouth = Math.Min(chunkBlockZ + 1, 15);
            var heightNorth = context.GetHeight(chunkBlockX, zNorth);
            var heightSouth = context.GetHeight(chunkBlockX, zSouth);
            if (heightSouth >= heightNorth + 4) return true;
            var xWest = Math.Max(chunkBlockX - 1, 0);
            var xEast = Math.Min(chunkBlockX + 1, 15);
            var heightWest = context.GetHeight(xWest, chunkBlockZ);
            var heightEast = context.GetHeight(xEast, chunkBlockZ);
            return heightWest >= heightEast + 4;
        }
    }

    //Hole is the surface erosion hole condition, maps to vanilla SurfaceRules.hole; a non-positive surfaceDepth is a hole
    public sealed class Hole : ConditionSource
    {
        public bool Test(Context context) => context.SurfaceDepth <= 0;
    }

    //Bandlands is the badlands band material, maps to vanilla SurfaceRules.bandlands
    //Looks up a pre-generated 192-long band table by y and noise offset
    public sealed class Bandlands : RuleSource
    {
        public BlockState? Apply(Context context, BlockState current) => context.GetBand();
    }
}
