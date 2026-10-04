using NetCraft.Game.Data;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseRouterData 密度路由数据对应原版 net.minecraft.world.level.levelgen.NoiseRouterData
//构造 overworld/nether/end/caves/floatingIslands/none 六个维度的 NoiseRouter 密度树
//主世界树按 shift/continents/erosion/ridge -> spline offset/factor/jaggedness -> depth -> initialDensity -> slopedCheese -> caves -> postProcess -> FinalDensity 链路组装
//不写入 DENSITY_FUNCTION 注册表直接返回 NoiseRouterNetCraft 暂不做 Codec 持久化
public static class NoiseRouterData
{
    //常量对齐原版 NoiseRouterData 静态字段
    public const float GlobalOffset = -0.50375f;
    private const float OreThickness = 0.08f;
    private const double VeininessFrequency = 1.5d;
    private const double NoodleSpacingAndStraightness = 1.5d;
    private const double SurfaceDensityThreshold = 1.5625d;
    private const double CheeseNoiseTarget = -0.703125d;
    public const double NoiseZero = 0.390625d;
    public const int IslandChunkDistance = 64;
    public const long IslandChunkDistanceSqr = 4096L;
    private const int DensityYAnchorBottom = -64;
    private const int DensityYAnchorTop = 320;
    private const double DensityYBottom = 1.5d;
    private const double DensityYTop = -1.5d;
    private const int OverworldBottomSlideHeight = 24;
    private const double BaseDensityMultiplier = 4.0d;

    //BlendingFactor 默认混合目标常量对应原版 BLENDING_FACTOR
    private static readonly DensityFunction BlendingFactor = DensityFunctions.ConstantValue(10.0d);
    //BlendingJaggedness 默认混合锯齿常量对应原版 BLENDING_JAGGEDNESS = zero
    private static readonly DensityFunction BlendingJaggedness = DensityFunctions.Zero();

    //Overworld 主世界路由对应原版 overworld
    //largeBiomes=true 切换 TEMPERATURE_LARGE 等大尺度噪声amplified=true 切换 OFFSET_AMPLIFIED 等放大样条
    public static NoiseRouter Overworld(Registry<NoiseParameters> noises, bool largeBiomes, bool amplified)
    {
        var barrierNoise = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.AquiferBarrier), 0.5);
        var fluidLevelFloodednessNoise = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.AquiferFluidLevelFloodedness), 0.67);
        var fluidLevelSpreadNoise = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.AquiferFluidLevelSpread), 0.7142857142857143);
        var lavaNoise = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.AquiferLava));

        var shiftX = DensityFunctions.FlatCache(DensityFunctions.Cache2D(DensityFunctions.ShiftA(noises.GetValueOrThrow(Noises.Shift))));
        var shiftZ = DensityFunctions.FlatCache(DensityFunctions.Cache2D(DensityFunctions.ShiftB(noises.GetValueOrThrow(Noises.Shift))));

        var temperature = DensityFunctions.ShiftedNoise2d(shiftX, shiftZ, 0.25,
            noises.GetValueOrThrow(largeBiomes ? Noises.TemperatureLarge : Noises.Temperature));
        var vegetation = DensityFunctions.ShiftedNoise2d(shiftX, shiftZ, 0.25,
            noises.GetValueOrThrow(largeBiomes ? Noises.VegetationLarge : Noises.Vegetation));

        var continents = DensityFunctions.FlatCache(DensityFunctions.ShiftedNoise2d(shiftX, shiftZ, 0.25,
            noises.GetValueOrThrow(Noises.Continentalness)));
        var erosion = DensityFunctions.FlatCache(DensityFunctions.ShiftedNoise2d(shiftX, shiftZ, 0.25,
            noises.GetValueOrThrow(Noises.Erosion)));
        var ridge = DensityFunctions.FlatCache(DensityFunctions.ShiftedNoise2d(shiftX, shiftZ, 0.25,
            noises.GetValueOrThrow(Noises.Ridge)));
        var ridgesFolded = PeaksAndValleys(ridge);

        var jaggedNoise = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.Jagged), 1500.0, 0.0);
        var (offset, factor, _, depth, slopedCheese) = RegisterTerrainNoises(noises, jaggedNoise, continents, erosion, ridge, ridgesFolded, amplified);

        var preliminarySurfaceLevel = PreliminarySurfaceLevel(offset, factor, amplified);
        var slopedCheeseCached = DensityFunctions.CacheOnce(slopedCheese);
        var surfaceWithEntrances = DensityFunctions.Min(slopedCheeseCached,
            DensityFunctions.Mul(DensityFunctions.ConstantValue(5.0), Entrances(noises, slopedCheeseCached)));
        var caves = DensityFunctions.RangeChoice(slopedCheeseCached, -1000000.0, SurfaceDensityThreshold,
            surfaceWithEntrances, Underground(noises, slopedCheeseCached));
        var fullNoise = DensityFunctions.Min(PostProcess(SlideOverworld(amplified, caves)), Noodle(noises));

        //VeinToggle/VeinRidged/VeinGap 矿物脉相关密度函数对应原版 overworld 末段
        var veinMinY = -64;
        var veinMaxY = 320;
        var y = DensityFunctions.YClampedGradient(-2048, 2048, -2048, 2048);
        var veinToggle = YLimitedInterpolatable(y, DensityFunctions.Noise(noises.GetValueOrThrow(Noises.OreVeininess), VeininessFrequency, VeininessFrequency), veinMinY, veinMaxY, 0);
        var veinA = YLimitedInterpolatable(y, DensityFunctions.Noise(noises.GetValueOrThrow(Noises.OreVeinA), 4.0, 4.0), veinMinY, veinMaxY, 0);
        var veinB = YLimitedInterpolatable(y, DensityFunctions.Noise(noises.GetValueOrThrow(Noises.OreVeinB), 4.0, 4.0), veinMinY, veinMaxY, 0);
        var veinRidged = DensityFunctions.Add(DensityFunctions.ConstantValue(-0.07999999821186066), DensityFunctions.Max(DensityFunctions.Abs(veinA), DensityFunctions.Abs(veinB)));
        var veinGap = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.OreGap));

        return new NoiseRouter(
            barrierNoise, fluidLevelFloodednessNoise, fluidLevelSpreadNoise, lavaNoise,
            temperature, vegetation, continents, erosion, depth, ridgesFolded,
            preliminarySurfaceLevel, fullNoise, veinToggle, veinRidged, veinGap);
    }

    //Nether 下界路由对应原版 nether
    public static NoiseRouter Nether(Registry<NoiseParameters> noises)
    {
        var temperature = DensityFunctions.ShiftedNoise2d(DensityFunctions.Zero(), DensityFunctions.Zero(), 0.25,
            noises.GetValueOrThrow(Noises.TemperatureNether));
        var vegetation = DensityFunctions.ShiftedNoise2d(DensityFunctions.Zero(), DensityFunctions.Zero(), 0.25,
            noises.GetValueOrThrow(Noises.VegetationNether));
        var slide = SlideNetherLike(noises, 0, 128);
        var fullNoise = PostProcess(slide);
        return SimpleRouter(fullNoise, temperature, vegetation);
    }

    //Caves 洞穴维度路由对应原版 caves
    public static NoiseRouter Caves(Registry<NoiseParameters> noises)
    {
        var slide = SlideNetherLike(noises, -64, 192);
        return SimpleRouter(PostProcess(slide));
    }

    //FloatingIslands 浮空岛维度路由对应原版 floatingIslands
    public static NoiseRouter FloatingIslands(Registry<NoiseParameters> noises)
    {
        var baseNoise = BlendedNoise.CreateUnseeded(0.25, 0.25, 80.0, 160.0, 4.0);
        var slide = SlideEndLike(baseNoise, 0, 256);
        return SimpleRouter(PostProcess(slide));
    }

    //End 末地路由对应原版 end
    public static NoiseRouter End(Registry<NoiseParameters> noises)
    {
        var islands = DensityFunctions.Cache2D(DensityFunctions.EndIslands(0L));
        var baseNoise = BlendedNoise.CreateUnseeded(0.25, 0.25, 80.0, 160.0, 4.0);
        var slopedCheeseEnd = DensityFunctions.Add(islands, baseNoise);
        var fullNoise = PostProcess(SlideEndLike(slopedCheeseEnd, 0, 128));
        return new NoiseRouter(
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(),
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(), islands,
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(), fullNoise,
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero());
    }

    //None 空路由对应原版 none
    public static NoiseRouter None()
        => SimpleRouter(DensityFunctions.Zero());

    //SimpleRouter 简化路由器对应原版 simpleRouter
    //15 字段除 fullNoise 全置 zerotemperature/vegetation 可选注入
    private static NoiseRouter SimpleRouter(DensityFunction fullNoise,
        DensityFunction? temperature = null, DensityFunction? vegetation = null)
        => new(
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(),
            temperature ?? DensityFunctions.Zero(), vegetation ?? DensityFunctions.Zero(),
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(),
            DensityFunctions.Zero(), fullNoise,
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero());

    //RegisterTerrainNoises 构造 offset/factor/jaggedness/depth/slopedCheese 五元组对应原版 registerTerrainNoises
    //返回元组供 Overworld 主流程引用中间函数不写入注册表
    private static (DensityFunction offset, DensityFunction factor, DensityFunction jaggedness, DensityFunction depth, DensityFunction slopedCheese) RegisterTerrainNoises(
        Registry<NoiseParameters> noises,
        DensityFunction jaggedNoise,
        DensityFunction continentsFunction,
        DensityFunction erosionFunction,
        DensityFunction ridge,
        DensityFunction ridgesFolded,
        bool amplified)
    {
        var offset = SplineWithBlending(
            DensityFunctions.Add(DensityFunctions.ConstantValue(-0.5037500262260437),
                DensityFunctions.Spline(TerrainProvider.OverworldOffset(continentsFunction, erosionFunction, ridgesFolded, amplified))),
            DensityFunctions.BlendOffset());
        var factor = SplineWithBlending(
            DensityFunctions.Spline(TerrainProvider.OverworldFactor(continentsFunction, erosionFunction, ridge, ridgesFolded, amplified)),
            BlendingFactor);
        var depth = OffsetToDepth(offset);
        var unscaledJaggedness = SplineWithBlending(
            DensityFunctions.Spline(TerrainProvider.OverworldJaggedness(continentsFunction, erosionFunction, ridge, ridgesFolded, amplified)),
            BlendingJaggedness);
        var jaggedness = DensityFunctions.FlatCache(DensityFunctions.Mul(unscaledJaggedness, DensityFunctions.HalfNegative(jaggedNoise)));
        var initialDensity = NoiseGradientDensity(factor, DensityFunctions.Add(depth, jaggedness));
        var baseNoise = BlendedNoise.CreateUnseeded(0.25, 0.125, 80.0, 160.0, 8.0);
        var slopedCheese = DensityFunctions.Add(initialDensity, baseNoise);
        return (offset, factor, jaggedness, depth, slopedCheese);
    }

    //OffsetToDepth 把 offset 加到 Y 梯度上得到 depth 对应原版 offsetToDepth
    private static DensityFunction OffsetToDepth(DensityFunction offset)
        => DensityFunctions.Add(DensityFunctions.YClampedGradient(DensityYAnchorBottom, DensityYAnchorTop, DensityYBottom, DensityYTop), offset);

    //PeaksAndValleys 山峰山谷变换对应原版 peaksAndValleys
    //公式 (|(|ridge| - 0.6667) - 0.3333|) * -3
    private static DensityFunction PeaksAndValleys(DensityFunction weirdness)
        => DensityFunctions.Mul(
            DensityFunctions.Add(
                DensityFunctions.Abs(DensityFunctions.Add(DensityFunctions.Abs(weirdness), DensityFunctions.ConstantValue(-0.6666666666666666))),
                DensityFunctions.ConstantValue(-0.3333333333333333)),
            DensityFunctions.ConstantValue(-3.0));

    //SplineWithBlending 样条混合 + 二级缓存对应原版 splineWithBlending
    private static DensityFunction SplineWithBlending(DensityFunction spline, DensityFunction blendingTarget)
    {
        var blended = DensityFunctions.Lerp(DensityFunctions.BlendAlpha(), blendingTarget, spline);
        return DensityFunctions.FlatCache(DensityFunctions.Cache2D(blended));
    }

    //NoiseGradientDensity 噪声梯度密度对应原版 noiseGradientDensity
    //output = 4 * ((depthWithJaggedness * factor).quarterNegative())
    private static DensityFunction NoiseGradientDensity(DensityFunction factor, DensityFunction depthWithJaggedness)
    {
        var gradientUnscaled = DensityFunctions.Mul(depthWithJaggedness, factor);
        return DensityFunctions.Mul(DensityFunctions.ConstantValue(BaseDensityMultiplier), DensityFunctions.QuarterNegative(gradientUnscaled));
    }

    //SlideOverworld 主世界 Y 轴滑变对应原版 slideOverworld
    //amplified 模式用更陡的 topSlide 与更柔的 bottomSlide
    private static DensityFunction SlideOverworld(bool isAmplified, DensityFunction caves)
        => Slide(caves, -64, 384, isAmplified ? 16 : 80, isAmplified ? 0 : 64, -0.078125d, 0, OverworldBottomSlideHeight, isAmplified ? 0.4d : 0.1171875d);

    //SlideNetherLike 下界式滑变对应原版 slideNetherLike
    private static DensityFunction SlideNetherLike(Registry<NoiseParameters> noises, int minY, int height)
    {
        var baseNoise = BlendedNoise.CreateUnseeded(0.25, 0.375, 80.0, 60.0, 8.0);
        return Slide(baseNoise, minY, height, 24, 0, 0.9375d, -8, 24, 2.5d);
    }

    //SlideEndLike 末地式滑变对应原版 slideEndLike
    private static DensityFunction SlideEndLike(DensityFunction caves, int minY, int height)
        => Slide(caves, minY, height, 72, -184, -23.4375d, 4, 32, -0.234375d);

    //Slide Y 轴双向滑变对应原版 slide
    //上方按 topFactor 从 1 到 0 lerp 到 topTarget下方按 bottomFactor 从 0 到 1 lerp 到 bottomTarget
    private static DensityFunction Slide(DensityFunction caves, int minY, int height,
        int topStartY, int topEndY, double topTarget,
        int bottomStartY, int bottomEndY, double bottomTarget)
    {
        var topFactor = DensityFunctions.YClampedGradient((minY + height) - topStartY, (minY + height) - topEndY, 1.0d, 0.0d);
        var noiseValue = DensityFunctions.Lerp(topFactor, topTarget, caves);
        var bottomFactor = DensityFunctions.YClampedGradient(minY + bottomStartY, minY + bottomEndY, 0.0d, 1.0d);
        return DensityFunctions.Lerp(bottomFactor, bottomTarget, noiseValue);
    }

    //PostProcess 后处理对应原版 postProcess
    //blendDensity + 插值 + 0.64 缩放 + squeeze 挤压
    private static DensityFunction PostProcess(DensityFunction slide)
    {
        var blended = DensityFunctions.BlendDensity(slide);
        return DensityFunctions.Squeeze(DensityFunctions.Mul(DensityFunctions.Interpolated(blended), DensityFunctions.ConstantValue(0.64d)));
    }

    //Underground 地下密度对应原版 underground
    //组合 spaghetti2D/entrances/cave_cheese/cave_layer 形成洞穴主密度
    private static DensityFunction Underground(Registry<NoiseParameters> noises, DensityFunction slopedCheese)
    {
        var spaghetti2DFunction = Spaghetti2D(noises);
        var spaghettiRoughnessFunction = SpaghettiRoughnessFunction(noises);
        var layerNoiseSource = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.CaveLayer), 8.0);
        var layerizedCavernsFunction = DensityFunctions.Mul(DensityFunctions.ConstantValue(4.0), DensityFunctions.Square(layerNoiseSource));
        var cheese = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.CaveCheese), 0.6666666666666666);
        var solidifiedCheeseWithTopSlide = DensityFunctions.Add(
            DensityFunctions.Add(DensityFunctions.ConstantValue(0.27), cheese).Clamp(-1.0d, 1.0d),
            DensityFunctions.Add(DensityFunctions.ConstantValue(1.5), DensityFunctions.Mul(DensityFunctions.ConstantValue(-0.64), slopedCheese)).Clamp(0.0d, 0.5d));
        var baseCaveDensity = DensityFunctions.Add(layerizedCavernsFunction, solidifiedCheeseWithTopSlide);
        var undergroundSubtractions = DensityFunctions.Min(
            DensityFunctions.Min(baseCaveDensity, Entrances(noises, slopedCheese)),
            DensityFunctions.Add(spaghetti2DFunction, spaghettiRoughnessFunction));
        var pillarsWithoutCutoff = Pillars(noises);
        var pillars = DensityFunctions.RangeChoice(pillarsWithoutCutoff, -1000000.0d, 0.03d, DensityFunctions.ConstantValue(-1000000.0d), pillarsWithoutCutoff);
        return DensityFunctions.Max(undergroundSubtractions, pillars);
    }

    //Entrances 入口密度对应原版 entrances
    private static DensityFunction Entrances(Registry<NoiseParameters> noises, DensityFunction slopedCheese)
    {
        var spaghetti3DRarityModulator = DensityFunctions.CacheOnce(DensityFunctions.Noise(noises.GetValueOrThrow(Noises.Spaghetti3DRarity), 2.0, 1.0));
        var spaghetti3DThicknessModulator = DensityFunctions.MappedNoise(noises.GetValueOrThrow(Noises.Spaghetti3DThickness), -0.065d, -0.088d);
        var spaghetti3DCave1 = QuantizedSpaghettiRarity.WrapRarity3d(spaghetti3DRarityModulator, noises.GetValueOrThrow(Noises.Spaghetti3D1));
        var spaghetti3DCave2 = QuantizedSpaghettiRarity.WrapRarity3d(spaghetti3DRarityModulator, noises.GetValueOrThrow(Noises.Spaghetti3D2));
        var spaghetti3DFunction = DensityFunctions.Add(DensityFunctions.Max(spaghetti3DCave1, spaghetti3DCave2), spaghetti3DThicknessModulator).Clamp(-1.0d, 1.0d);
        var spaghettiRoughnessFunction = SpaghettiRoughnessFunction(noises);
        var bigEntranceNoiseSource = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.CaveEntrance), 0.75, 0.5);
        var bigEntrancesFunction = DensityFunctions.Add(
            DensityFunctions.Add(bigEntranceNoiseSource, DensityFunctions.ConstantValue(0.37)),
            DensityFunctions.YClampedGradient(-10, 30, 0.3d, 0.0d));
        return DensityFunctions.CacheOnce(DensityFunctions.Min(bigEntrancesFunction, DensityFunctions.Add(spaghettiRoughnessFunction, spaghetti3DFunction)));
    }

    //Noodle 面条洞穴密度对应原版 noodle
    private static DensityFunction Noodle(Registry<NoiseParameters> noises)
    {
        var y = DensityFunctions.YClampedGradient(-2048, 2048, -2048, 2048);
        var noodleToggle = YLimitedInterpolatable(y, DensityFunctions.Noise(noises.GetValueOrThrow(Noises.Noodle), 1.0, 1.0), -60, 320, -1);
        var noodleThickness = YLimitedInterpolatable(y, DensityFunctions.MappedNoise(noises.GetValueOrThrow(Noises.NoodleThickness), 1.0, 1.0, -0.05d, -0.1d), -60, 320, 0);
        var noodleRidgeA = YLimitedInterpolatable(y, DensityFunctions.Noise(noises.GetValueOrThrow(Noises.NoodleRidgeA), 2.6666666666666665d, 2.6666666666666665d), -60, 320, 0);
        var noodleRidgeB = YLimitedInterpolatable(y, DensityFunctions.Noise(noises.GetValueOrThrow(Noises.NoodleRidgeB), 2.6666666666666665d, 2.6666666666666665d), -60, 320, 0);
        var noodleRidged = DensityFunctions.Mul(DensityFunctions.ConstantValue(1.5), DensityFunctions.Max(DensityFunctions.Abs(noodleRidgeA), DensityFunctions.Abs(noodleRidgeB)));
        return DensityFunctions.RangeChoice(noodleToggle, -1000000.0d, 0.0d, DensityFunctions.ConstantValue(64.0d), DensityFunctions.Add(noodleThickness, noodleRidged));
    }

    //Pillars 柱状密度对应原版 pillars
    private static DensityFunction Pillars(Registry<NoiseParameters> noises)
    {
        var pillarNoiseSource = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.Pillar), 25.0, 0.3);
        var pillarRarenessModulator = DensityFunctions.MappedNoise(noises.GetValueOrThrow(Noises.PillarRareness), 0.0d, -2.0d);
        var pillarThicknessModulator = DensityFunctions.MappedNoise(noises.GetValueOrThrow(Noises.PillarThickness), 0.0d, 1.1d);
        var pillarsWithRareness = DensityFunctions.Add(DensityFunctions.Mul(pillarNoiseSource, DensityFunctions.ConstantValue(2.0)), pillarRarenessModulator);
        return DensityFunctions.CacheOnce(DensityFunctions.Mul(pillarsWithRareness, DensityFunctions.Cube(pillarThicknessModulator)));
    }

    //Spaghetti2D 二维意面洞穴密度对应原版 spaghetti2D
    private static DensityFunction Spaghetti2D(Registry<NoiseParameters> noises)
    {
        var spaghetti2DRarityModulator = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.Spaghetti2DModulator), 2.0, 1.0);
        var spaghetti2DCave = QuantizedSpaghettiRarity.WrapRarity2d(spaghetti2DRarityModulator, noises.GetValueOrThrow(Noises.Spaghetti2D));
        var spaghetti2DElevationModulator = DensityFunctions.MappedNoise(noises.GetValueOrThrow(Noises.Spaghetti2DElevation), 0.0d, (-64) / 8, 8.0d);
        var spaghetti2DThicknessModulator = DensityFunctions.CacheOnce(DensityFunctions.MappedNoise(noises.GetValueOrThrow(Noises.Spaghetti2DThickness), 2.0, 1.0, -0.6d, -1.3d));
        var slopedSpaghetti = DensityFunctions.Add(DensityFunctions.FlatCache(spaghetti2DElevationModulator),
            DensityFunctions.YClampedGradient(-64, 320, 8.0d, -40.0d)).Abs();
        var layerRidged = DensityFunctions.Cube(DensityFunctions.Add(slopedSpaghetti, spaghetti2DThicknessModulator));
        var caveNoise = DensityFunctions.Add(spaghetti2DCave, DensityFunctions.Mul(DensityFunctions.ConstantValue(0.083d), spaghetti2DThicknessModulator));
        return DensityFunctions.Max(caveNoise, layerRidged).Clamp(-1.0d, 1.0d);
    }

    //SpaghettiRoughnessFunction 意面粗糙度函数对应原版 spaghettiRoughnessFunction
    private static DensityFunction SpaghettiRoughnessFunction(Registry<NoiseParameters> noises)
    {
        var spaghettiRoughnessNoise = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.SpaghettiRoughness));
        var spaghettiRoughnessModulator = DensityFunctions.MappedNoise(noises.GetValueOrThrow(Noises.SpaghettiRoughnessModulator), 0.0d, -0.1d);
        return DensityFunctions.CacheOnce(DensityFunctions.Mul(spaghettiRoughnessModulator, DensityFunctions.Add(spaghettiRoughnessNoise.Abs(), DensityFunctions.ConstantValue(-0.4d))));
    }

    //PreliminarySurfaceLevel 预备表面等级对应原版 preliminarySurfaceLevel
    private static DensityFunction PreliminarySurfaceLevel(DensityFunction offset, DensityFunction factor, bool amplified)
    {
        var cachedFactor = DensityFunctions.Cache2D(factor);
        var cachedOffset = DensityFunctions.Cache2D(offset);
        var upperBound = Remap(
            DensityFunctions.Add(
                DensityFunctions.Mul(DensityFunctions.ConstantValue(0.2734375), DensityFunctions.Invert(cachedFactor)),
                DensityFunctions.Mul(DensityFunctions.ConstantValue(-1.0), cachedOffset)),
            1.5d, DensityYTop, -64.0d, 320.0d).Clamp(-40.0d, 320.0d);
        var density = DensityFunctions.Add(
            SlideOverworld(amplified,
                DensityFunctions.Add(NoiseGradientDensity(cachedFactor, OffsetToDepth(cachedOffset)), DensityFunctions.ConstantValue(CheeseNoiseTarget)).Clamp(-64.0d, 64.0d)),
            DensityFunctions.ConstantValue(-NoiseZero));
        return DensityFunctions.FindTopSurface(density, upperBound, -64, NoiseSettings.Overworld.GetCellHeight());
    }

    //YLimitedInterpolatable Y 限制插值对应原版 yLimitedInterpolatable
    //y 落在 [minYInclusive, maxYInclusive] 时返回 whenInRange 否则返回常量 whenOutOfRange
    private static DensityFunction YLimitedInterpolatable(DensityFunction y, DensityFunction whenInRange,
        int minYInclusive, int maxYInclusive, int whenOutOfRange)
        => DensityFunctions.Interpolated(
            DensityFunctions.RangeChoice(y, minYInclusive, maxYInclusive + 1, whenInRange, DensityFunctions.ConstantValue(whenOutOfRange)));

    //Remap 线性重映射对应原版 remap
    //把 input 从 [fromMin, fromMax] 映射到 [toMin, toMax]
    private static DensityFunction Remap(DensityFunction input, double fromMin, double fromMax, double toMin, double toMax)
    {
        var factor = (toMax - toMin) / (fromMax - fromMin);
        var offset = toMin - (fromMin * factor);
        return DensityFunctions.Add(DensityFunctions.Mul(input, DensityFunctions.ConstantValue(factor)), DensityFunctions.ConstantValue(offset));
    }

    //QuantizedSpaghettiRarity 量化意面稀有度对应原版 QuantizedSpaghettiRarity 嵌套类
    //按 input 值分段选择不同稀有度的噪声函数
    private static class QuantizedSpaghettiRarity
    {
        //WrapRarity2d 二维意面稀有度包装对应原版 wrapRarity2d
        public static DensityFunction WrapRarity2d(DensityFunction input, NoiseParameters noise)
            => DensityFunctions.Abs(DensityFunctions.IntervalSelect(input,
                new[] { -0.75d, -0.5d, 0.5d, 0.75d },
                new[]
                {
                    NoiseFunctionForRarity(noise, 0.5d),
                    NoiseFunctionForRarity(noise, 0.75d),
                    NoiseFunctionForRarity(noise, 1.0d),
                    NoiseFunctionForRarity(noise, 2.0d),
                    NoiseFunctionForRarity(noise, 3.0d)
                }));

        //WrapRarity3d 三维意面稀有度包装对应原版 wrapRarity3d
        public static DensityFunction WrapRarity3d(DensityFunction input, NoiseParameters noise)
            => DensityFunctions.Abs(DensityFunctions.IntervalSelect(input,
                new[] { -0.5d, 0.0d, 0.5d },
                new[]
                {
                    NoiseFunctionForRarity(noise, 0.75d),
                    NoiseFunctionForRarity(noise, 1.0d),
                    NoiseFunctionForRarity(noise, 1.5d),
                    NoiseFunctionForRarity(noise, 2.0d)
                }));

        //NoiseFunctionForRarity 稀有度噪声函数对应原版 noiseFunctionForRarity
        //rarity 控制频率倒数与缩放
        private static DensityFunction NoiseFunctionForRarity(NoiseParameters noise, double rarity)
            => DensityFunctions.Mul(DensityFunctions.ConstantValue(rarity), DensityFunctions.Noise(noise, 1.0 / rarity, 1.0 / rarity));
    }
}
