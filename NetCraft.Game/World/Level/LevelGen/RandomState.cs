using System.Collections.Concurrent;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//RandomState 随机状态桥接 seed 与 NoiseRouter对应原版 net.minecraft.world.level.levelgen.RandomState
//构造时 mapAll 密度树把所有 NoiseHolder 替换为实例化的 NormalNoise把 BlendedNoise 注入 terrain 随机源
//提供 router/sampler/aquiferRandom/oreRandom/getOrCreateNoise 给 NoiseChunk/Aquifer 使用
public sealed class RandomState
{
    private readonly PositionalRandomFactory _random;
    private readonly Registry<NoiseParameters> _noises;
    //Seed 世界种子原值 地表阶段的群系模糊距离要用它派生 对应原版存放的种子
    public long Seed { get; }
    public NoiseRouter Router { get; }
    public Climate.Sampler Sampler { get; }
    //SurfaceSystem 世界级单例 与原版一致在构造时就建好 地表阶段直接用
    public SurfaceSystem SurfaceSystem { get; }
    private readonly PositionalRandomFactory _aquiferRandom;
    private readonly PositionalRandomFactory _oreRandom;
    //噪声实例与随机工厂缓存对应原版 ConcurrentHashMap
    //区块生成分布在多个线程上 同一个 key 会被并发访问
    private readonly ConcurrentDictionary<ResourceKey<NoiseParameters>, NormalNoise> _noiseInstances = new();
    private readonly ConcurrentDictionary<Identifier, PositionalRandomFactory> _positionalRandoms = new();

    //Create 工厂对应原版 create
    public static RandomState Create(NoiseGeneratorSettings settings, Registry<NoiseParameters> noises, long seed)
        => new(settings, noises, seed);

    private RandomState(NoiseGeneratorSettings settings, Registry<NoiseParameters> noises, long seed)
    {
        Seed = seed;
        _random = RandomSource.Create(seed).ForkPositional();
        _noises = noises;
        _aquiferRandom = _random.FromHashOf("aquifer").ForkPositional();
        _oreRandom = _random.FromHashOf("ore").ForkPositional();
        SurfaceSystem = new SurfaceSystem(this, settings.DefaultBlock, settings.SeaLevel, _random);

        var wiringHelper = new NoiseWiringHelper(this, settings.UseLegacyRandomSource, seed);
        Router = settings.NoiseRouter.MapAll(wiringHelper);

        //noiseFlattener 展开剩余 HolderHolder 与 Marker 节点供 Climate.Sampler 用
        var flattener = new NoiseFlattener();
        Sampler = new Climate.NoiseRouterSampler(Router);
        _ = flattener;
    }

    //GetOrCreateNoise 按键查/构造 NormalNoise对应原版 getOrCreateNoise
    //缓存避免重复实例化保证同 key 返回同一 NormalNoise 实例
    //原版噪声缓存是 ConcurrentHashMap + computeIfAbsent 区块生成跑在多个线程上这里要并发安全
    public NormalNoise GetOrCreateNoise(ResourceKey<NoiseParameters> key)
        => _noiseInstances.GetOrAdd(key, k => Noises.Instantiate(_noises, _random, k));

    //GetOrCreateRandomFactory 按名查/派生 PositionalRandomFactory对应原版 getOrCreateRandomFactory
    public PositionalRandomFactory GetOrCreateRandomFactory(Identifier name)
        => _positionalRandoms.GetOrAdd(name, n => _random.FromHashOf(n.ToString()).ForkPositional());

    public PositionalRandomFactory AquiferRandom => _aquiferRandom;
    public PositionalRandomFactory OreRandom => _oreRandom;
}

//NoiseWiringHelper NoiseHolder/BlendedNoise 装配 visitor对应原版 RandomState 内部匿名 Visitor
//visitNoise 把 NoiseHolder 替换为实例化的 NormalNoise（TEMPERATURE_NETHER/VEGETATION_NETHER 走 legacy 路径）
//apply 对 BlendedNoise 注入新随机源对 EndIslandDensityFunction 替换为带 seed 的新实例
internal sealed class NoiseWiringHelper : Visitor
{
    private readonly RandomState _owner;
    private readonly bool _useLegacyInit;
    private readonly long _seed;
    private readonly Dictionary<DensityFunction, DensityFunction> _wrapped = new();

    public NoiseWiringHelper(RandomState owner, bool useLegacyInit, long seed)
    {
        _owner = owner;
        _useLegacyInit = useLegacyInit;
        _seed = seed;
    }

    //NewLegacyInstance 按 seedOffset 派生 LegacyRandomSource对应原版 newLegacyInstance
    private RandomSource NewLegacyInstance(long seedOffset) => new LegacyRandomSource(_seed + seedOffset);

    public NoiseHolder VisitNoise(NoiseHolder noise)
    {
        var noiseData = noise.NoiseData;
        if (noiseData is null) return noise;
        //NetCraft 暂无 ResourceKey 比较 HolderData 是否 NETHER 路径改通过 NormalNoise.CreateLegacyNetherBiome 兼容下界
        //原版 is(Noises.TEMPERATURE_NETHER) 走 LegacyNetherBiome 路径此处统一走标准实例化
        var instantiated = _owner.GetOrCreateNoise(GetKeyForData(noiseData));
        return new NoiseHolder(noiseData, instantiated);
    }

    //GetKeyForData 从 NoiseParameters 反查 ResourceKey对应原版 noiseData.unwrapKey().orElseThrow
    //NetCraft BuiltInRegistries.NOISE 提供 GetKey 反查
    private ResourceKey<NoiseParameters> GetKeyForData(NoiseParameters data)
    {
        foreach (var key in BuiltInRegistries.NOISE.RegistryKeySet)
        {
            if (BuiltInRegistries.NOISE.GetValue(key) == data)
                return key;
        }
        throw new InvalidOperationException("NoiseParameters not registered");
    }

    public DensityFunction Apply(DensityFunction input)
    {
        if (_wrapped.TryGetValue(input, out var cached)) return cached;
        var result = WrapNew(input);
        _wrapped[input] = result;
        return result;
    }

    //WrapNew 节点替换逻辑对应原版 wrapNew
    private DensityFunction WrapNew(DensityFunction function)
    {
        if (function is BlendedNoise blended)
        {
            var terrainRandom = _useLegacyInit
                ? NewLegacyInstance(0L)
                : _owner.GetOrCreateRandomFactory(Identifier.WithDefaultNamespace("terrain")).FromSeed(0L);
            return blended.WithNewRandom(terrainRandom);
        }
        if (function is EndIslandDensityFunction)
        {
            return new EndIslandDensityFunction(_seed);
        }
        return function;
    }
}

//NoiseFlattener HolderHolder/Marker 展开器对应原版 RandomState 第二个匿名 Visitor
//展开 HolderHolder 为内部函数展开 Marker 为内部 wrapped 节省 Climate.Sampler 调用层级
internal sealed class NoiseFlattener : Visitor
{
    private readonly Dictionary<DensityFunction, DensityFunction> _wrapped = new();

    public NoiseHolder VisitNoise(NoiseHolder noise) => noise;

    public DensityFunction Apply(DensityFunction input)
    {
        if (_wrapped.TryGetValue(input, out var cached)) return cached;
        var result = WrapNew(input);
        _wrapped[input] = result;
        return result;
    }

    private static DensityFunction WrapNew(DensityFunction function)
    {
        if (function is HolderHolder holder)
            return holder.Function;
        if (function is MarkerNode marker)
            return marker.Wrapped;
        return function;
    }
}
