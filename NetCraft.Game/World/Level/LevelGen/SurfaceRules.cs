using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util;
using NetCraft.Util.Random;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Game.World.Level.LevelGen;

//SurfaceRules 表面规则系统对应原版 net.minecraft.world.level.levelgen.SurfaceRules
//提供条件源 ConditionSource 与规则源 RuleSource 两类可序列化对象
//MATERIAL_CONDITION/MATERIAL_RULE 用 MapCodec<object> 弱类型对齐跨层方案
//求值按"每个方块现算"实现 不照搬原版的 Condition 懒求值对象层 结果等价
public static class SurfaceRules
{
    //Context 规则求值上下文 由 SurfaceSystem 每列 updateXZ 一次 每方块 updateY 一次
    //字段语义对齐原版 SurfaceRules.Context 各 Condition 的求值公式直接读这些字段
    public sealed class Context
    {
        //HowFarBelowPreliminarySurface 初步地表以下仍参与构建的深度对应原版同名常量
        private const int HowFarBelowPreliminarySurface = 8;
        //SurfaceCellSize 初步地表按 16 格格子取四角插值对应原版 SURFACE_CELL_SIZE
        private const int SurfaceCellSize = 16;

        //DefaultMinY/DefaultHeight 主世界维度高度范围 供 VerticalAnchor 解算兜底
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
        //SurfaceDepth 该列地表厚度噪声 原版 getSurfaceDepth 结果
        public int SurfaceDepth { get; private set; }
        //WaterHeight 该列最近一次水面高度 无流体时为 int.MinValue 对应原版 waterHeight
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

        //UpdateXZ 换列时调用 重算 surfaceDepth 并让按列缓存失效
        public void UpdateXZ(int blockX, int blockZ)
        {
            BlockX = blockX;
            BlockZ = blockZ;
            SurfaceDepth = _system.GetSurfaceDepth(blockX, blockZ);
            _hasSurfaceSecondary = false;
            _hasMinSurfaceLevel = false;
        }

        //UpdateY 换方块时调用 生物群系按 y 变化因此一并失效
        public void UpdateY(int stoneDepthAbove, int stoneDepthBelow, int waterHeight, int blockY)
        {
            BlockY = blockY;
            WaterHeight = waterHeight;
            StoneDepthBelow = stoneDepthBelow;
            StoneDepthAbove = stoneDepthAbove;
            _biome = null;
        }

        //GetSurfaceSecondary 次级地表噪声按列懒算 对应原版 getSurfaceSecondary
        public double GetSurfaceSecondary()
        {
            if (!_hasSurfaceSecondary)
            {
                _surfaceSecondary = _system.GetSurfaceSecondary(BlockX, BlockZ);
                _hasSurfaceSecondary = true;
            }
            return _surfaceSecondary;
        }

        //GetMinSurfaceLevel 初步地表下限 取 16 格格子四角插值再减 surfaceDepth 与 8 格余量
        //对应原版 getMinSurfaceLevel 无 NoiseChunk 时退化为海平面 仅测试路径会走到
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

        //GetBiome 当前方块所在群系 按 y 懒取对应原版 getBiome
        public Biome GetBiome()
        {
            if (_biome is null)
                _biome = _biomeGetter?.Invoke(BlockX, BlockY, BlockZ) ?? Biome.Plains;
            return _biome;
        }

        public int GetSeaLevel() => _system.SeaLevel;

        //GetBand 恶地条带材质 对应原版 bandlands 规则引用的 surfaceSystem.getBand
        public BlockState GetBand() => _system.GetBand(BlockX, BlockY, BlockZ);

        //SampleNoise 按噪声数据取采样器 2d 只按 XZ 缓存 3d 按 XY Z缓存对应原版 getNoiseSampler
        public double SampleNoise(NoiseHolder noise, bool is3d)
        {
            if (!_samplers.TryGetValue(noise, out var sampler))
            {
                sampler = new NoiseSampler(this, noise, is3d);
                _samplers[noise] = sampler;
            }
            return sampler.Value;
        }

        //GetRandomFactory 取命名位置随机工厂供 vertical_gradient 抽概率
        public PositionalRandomFactory? GetRandomFactory(Identifier name)
            => _system.RandomState?.GetOrCreateRandomFactory(name);

        //GetHeight 取该列 WORLD_SURFACE_WG 高度供 steep 条件比较邻列坡度
        public int GetHeight(int localX, int localZ)
            => _chunk?.GetHeight(HeightmapRegistry.Types.WorldSurfaceWg, localX, localZ) ?? int.MinValue;

        //NoiseSampler 单个噪声的采样缓存 同一坐标重复求值时只算一次
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

    //SurfaceType 石头上/下表面朝向对应原版 CaveSurface
    public enum SurfaceType
    {
        Floor,
        Ceiling
    }

    //ConditionSource 条件源注册到 MATERIAL_CONDITION 注册表
    //实现按 Context 决定是否进入某条规则分支
    public interface ConditionSource
    {
        bool Test(Context context);
    }

    //RuleSource 规则源注册到 MATERIAL_RULE 注册表
    //返回替换后的 BlockState返回 null 表示不替换
    public interface RuleSource
    {
        BlockState? Apply(Context context, BlockState current);
    }

    //BlockStateRule 持有固定 BlockState 直接替换对应原版 SurfaceRules.state
    public sealed class BlockStateRule : RuleSource
    {
        public BlockState State { get; }

        public BlockStateRule(BlockState state)
        {
            State = state;
        }

        public BlockState? Apply(Context context, BlockState current) => State;
    }

    //IfTrue 条件成立时应用嵌套规则对应原版 SurfaceRules.ifTrue
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

    //Sequence 顺序规则列表首个命中即返回对应原版 SurfaceRules.sequence
    //不是后面的覆盖前面的 基岩这类排在前面的规则必须能挡住后面的 deepslate
    public sealed class Sequence : RuleSource
    {
        //Rules 规则数组 每个方块都要从头走一遍 存数组才能走索引路径而不是装箱的接口枚举器
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

    //AbovePreliminarySurface 测试 y 是否在初步地表下限之上对应原版 above_preliminary_surface
    public sealed class AbovePreliminarySurface : ConditionSource
    {
        public bool Test(Context context) => context.BlockY >= context.GetMinSurfaceLevel();
    }

    //StoneDepth 测试距地表石头深度是否在阈值内对应原版 SurfaceRules.stoneDepth
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

        //Test 天花板取 stoneDepthBelow 地板取 stoneDepthAbove 阈值含 1 + offset + 两级地表厚度
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

    //VerticalGradient 按 y 递增概率出现对应原版 vertical_gradient
    //true_at_and_below 以下恒真 false_at_and_above 以上恒假 中间按位置随机取概率
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

        //VerticalGradient 不带随机名的简化构造 中间段用默认位置随机源
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

    //Not 取反条件对应原版 SurfaceRules.not
    public sealed class Not : ConditionSource
    {
        public ConditionSource Inner { get; }

        public Not(ConditionSource inner)
        {
            Inner = inner;
        }

        public bool Test(Context context) => !Inner.Test(context);
    }

    //BiomeCondition 测试当前生物群系是否匹配对应原版 SurfaceRules.biome
    //biome_is 既可以是显式群系列表 也可以是 "#命名空间:标签" 形式
    public sealed class BiomeCondition : ConditionSource
    {
        //Biomes 显式列出的群系 标签形式时为空
        //每个方块都要比一遍 存数组才能走索引路径而不是装箱的接口枚举器
        public Biome[] Biomes { get; }
        //Tag 标签引用 对应原版 biome_is 写成 "#minecraft:is_forest"
        public TagKey<Biome>? Tag { get; }
        //TagRegistry 解析标签用的群系注册表 标签形式时非空
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
                //标签没绑定内容时判不匹配 等价原版空标签语义
                var set = TagRegistry?.Get(Tag);
                return set is { IsBound: true } && set.Contains(TagRegistry!.WrapAsHolder(biome));
            }
            foreach (var b in Biomes)
                if (b.Id == biome.Id) return true;
            return false;
        }
    }

    //NoiseThreshold 噪声阈值条件对应原版 SurfaceRules.noiseThreshold
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

    //Water 水面条件对应原版 SurfaceRules.water
    //该列没有流体时恒真 有流体时只有在指定偏移以上才真
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

    //YAbove y 高于锚点条件对应原版 SurfaceRules.yAbove
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

    //Temperature 低温条件对应原版 SurfaceRules.temperature 成立表示该处冷到下雪
    public sealed class Temperature : ConditionSource
    {
        public bool Test(Context context)
            => BiomeTemperature.ColdEnoughToSnow(context.GetBiome(), context.BlockX, context.BlockY,
                context.BlockZ, context.GetSeaLevel());
    }

    //Steep 陡坡条件对应原版 SurfaceRules.steep 南北或东西邻列高度差达 4 即算陡
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

    //Hole 地表侵蚀空洞条件对应原版 SurfaceRules.hole surfaceDepth 非正即为空洞
    public sealed class Hole : ConditionSource
    {
        public bool Test(Context context) => context.SurfaceDepth <= 0;
    }

    //Bandlands 恶地条带材质对应原版 SurfaceRules.bandlands
    //按 y 与噪声偏移查预生成的 192 长条带表
    public sealed class Bandlands : RuleSource
    {
        public BlockState? Apply(Context context, BlockState current) => context.GetBand();
    }
}
