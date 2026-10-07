using NetCraft.Game.Data;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseRouterData density router data, maps to vanilla net.minecraft.world.level.levelgen.NoiseRouterData
//Builds the NoiseRouter density trees for the six dimensions overworld/nether/end/caves/floatingIslands/none
//The overworld tree is assembled along shift/continents/erosion/ridge -> spline offset/factor/jaggedness -> depth -> initialDensity -> slopedCheese -> caves -> postProcess -> FinalDensity
//Not written into the DENSITY_FUNCTION registry and returned directly as a NoiseRouter; NetCraft does not persist through Codec yet
public static class NoiseRouterData
{
    //Constants aligned with the vanilla NoiseRouterData static fields
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

    //BlendingFactor default blending target constant, maps to vanilla BLENDING_FACTOR
    private static readonly DensityFunction BlendingFactor = DensityFunctions.ConstantValue(10.0d);
    //BlendingJaggedness default blending jaggedness constant, maps to vanilla BLENDING_JAGGEDNESS = zero
    private static readonly DensityFunction BlendingJaggedness = DensityFunctions.Zero();

    //Overworld overworld router, maps to vanilla overworld
    //largeBiomes=true switches to large-scale noise such as TEMPERATURE_LARGE; amplified=true switches to amplified splines such as OFFSET_AMPLIFIED
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

        //VeinToggle/VeinRidged/VeinGap ore vein density functions, matching the tail of vanilla overworld
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

    //Nether the Nether router, maps to vanilla nether
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

    //Caves the caves dimension router, maps to vanilla caves
    public static NoiseRouter Caves(Registry<NoiseParameters> noises)
    {
        var slide = SlideNetherLike(noises, -64, 192);
        return SimpleRouter(PostProcess(slide));
    }

    //FloatingIslands the floating islands dimension router, maps to vanilla floatingIslands
    public static NoiseRouter FloatingIslands(Registry<NoiseParameters> noises)
    {
        var baseNoise = BlendedNoise.CreateUnseeded(0.25, 0.25, 80.0, 160.0, 4.0);
        var slide = SlideEndLike(baseNoise, 0, 256);
        return SimpleRouter(PostProcess(slide));
    }

    //End the End router, maps to vanilla end
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

    //None empty router, maps to vanilla none
    public static NoiseRouter None()
        => SimpleRouter(DensityFunctions.Zero());

    //SimpleRouter simplified router, maps to vanilla simpleRouter
    //All 15 fields are zero except fullNoise; temperature/vegetation are optional injections
    private static NoiseRouter SimpleRouter(DensityFunction fullNoise,
        DensityFunction? temperature = null, DensityFunction? vegetation = null)
        => new(
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(),
            temperature ?? DensityFunctions.Zero(), vegetation ?? DensityFunctions.Zero(),
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero(),
            DensityFunctions.Zero(), fullNoise,
            DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero());

    //RegisterTerrainNoises builds the offset/factor/jaggedness/depth/slopedCheese tuple, maps to vanilla registerTerrainNoises
    //Returns the tuple for the Overworld main flow to reference the intermediate functions without registering them
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

    //OffsetToDepth adds offset to the Y gradient to get depth, maps to vanilla offsetToDepth
    private static DensityFunction OffsetToDepth(DensityFunction offset)
        => DensityFunctions.Add(DensityFunctions.YClampedGradient(DensityYAnchorBottom, DensityYAnchorTop, DensityYBottom, DensityYTop), offset);

    //PeaksAndValleys peaks-and-valleys transform, maps to vanilla peaksAndValleys
    //Formula (|(|ridge| - 0.6667) - 0.3333|) * -3
    private static DensityFunction PeaksAndValleys(DensityFunction weirdness)
        => DensityFunctions.Mul(
            DensityFunctions.Add(
                DensityFunctions.Abs(DensityFunctions.Add(DensityFunctions.Abs(weirdness), DensityFunctions.ConstantValue(-0.6666666666666666))),
                DensityFunctions.ConstantValue(-0.3333333333333333)),
            DensityFunctions.ConstantValue(-3.0));

    //SplineWithBlending spline blending + two-level caching, maps to vanilla splineWithBlending
    private static DensityFunction SplineWithBlending(DensityFunction spline, DensityFunction blendingTarget)
    {
        var blended = DensityFunctions.Lerp(DensityFunctions.BlendAlpha(), blendingTarget, spline);
        return DensityFunctions.FlatCache(DensityFunctions.Cache2D(blended));
    }

    //NoiseGradientDensity noise gradient density, maps to vanilla noiseGradientDensity
    //output = 4 * ((depthWithJaggedness * factor).quarterNegative())
    private static DensityFunction NoiseGradientDensity(DensityFunction factor, DensityFunction depthWithJaggedness)
    {
        var gradientUnscaled = DensityFunctions.Mul(depthWithJaggedness, factor);
        return DensityFunctions.Mul(DensityFunctions.ConstantValue(BaseDensityMultiplier), DensityFunctions.QuarterNegative(gradientUnscaled));
    }

    //SlideOverworld overworld Y-axis slide, maps to vanilla slideOverworld
    //Amplified mode uses a steeper topSlide and a softer bottomSlide
    private static DensityFunction SlideOverworld(bool isAmplified, DensityFunction caves)
        => Slide(caves, -64, 384, isAmplified ? 16 : 80, isAmplified ? 0 : 64, -0.078125d, 0, OverworldBottomSlideHeight, isAmplified ? 0.4d : 0.1171875d);

    //SlideNetherLike Nether-style slide, maps to vanilla slideNetherLike
    private static DensityFunction SlideNetherLike(Registry<NoiseParameters> noises, int minY, int height)
    {
        var baseNoise = BlendedNoise.CreateUnseeded(0.25, 0.375, 80.0, 60.0, 8.0);
        return Slide(baseNoise, minY, height, 24, 0, 0.9375d, -8, 24, 2.5d);
    }

    //SlideEndLike End-style slide, maps to vanilla slideEndLike
    private static DensityFunction SlideEndLike(DensityFunction caves, int minY, int height)
        => Slide(caves, minY, height, 72, -184, -23.4375d, 4, 32, -0.234375d);

    //Slide bidirectional Y-axis slide, maps to vanilla slide
    //The top lerps from 1 to 0 by topFactor toward topTarget; the bottom lerps from 0 to 1 by bottomFactor toward bottomTarget
    private static DensityFunction Slide(DensityFunction caves, int minY, int height,
        int topStartY, int topEndY, double topTarget,
        int bottomStartY, int bottomEndY, double bottomTarget)
    {
        var topFactor = DensityFunctions.YClampedGradient((minY + height) - topStartY, (minY + height) - topEndY, 1.0d, 0.0d);
        var noiseValue = DensityFunctions.Lerp(topFactor, topTarget, caves);
        var bottomFactor = DensityFunctions.YClampedGradient(minY + bottomStartY, minY + bottomEndY, 0.0d, 1.0d);
        return DensityFunctions.Lerp(bottomFactor, bottomTarget, noiseValue);
    }

    //PostProcess post-processing, maps to vanilla postProcess
    //blendDensity + interpolation + 0.64 scaling + squeeze
    private static DensityFunction PostProcess(DensityFunction slide)
    {
        var blended = DensityFunctions.BlendDensity(slide);
        return DensityFunctions.Squeeze(DensityFunctions.Mul(DensityFunctions.Interpolated(blended), DensityFunctions.ConstantValue(0.64d)));
    }

    //Underground underground density, maps to vanilla underground
    //Combines spaghetti2D/entrances/cave_cheese/cave_layer into the main cave density
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

    //Entrances entrance density, maps to vanilla entrances
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

    //Noodle noodle cave density, maps to vanilla noodle
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

    //Pillars pillar density, maps to vanilla pillars
    private static DensityFunction Pillars(Registry<NoiseParameters> noises)
    {
        var pillarNoiseSource = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.Pillar), 25.0, 0.3);
        var pillarRarenessModulator = DensityFunctions.MappedNoise(noises.GetValueOrThrow(Noises.PillarRareness), 0.0d, -2.0d);
        var pillarThicknessModulator = DensityFunctions.MappedNoise(noises.GetValueOrThrow(Noises.PillarThickness), 0.0d, 1.1d);
        var pillarsWithRareness = DensityFunctions.Add(DensityFunctions.Mul(pillarNoiseSource, DensityFunctions.ConstantValue(2.0)), pillarRarenessModulator);
        return DensityFunctions.CacheOnce(DensityFunctions.Mul(pillarsWithRareness, DensityFunctions.Cube(pillarThicknessModulator)));
    }

    //Spaghetti2D 2D spaghetti cave density, maps to vanilla spaghetti2D
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

    //SpaghettiRoughnessFunction spaghetti roughness function, maps to vanilla spaghettiRoughnessFunction
    private static DensityFunction SpaghettiRoughnessFunction(Registry<NoiseParameters> noises)
    {
        var spaghettiRoughnessNoise = DensityFunctions.Noise(noises.GetValueOrThrow(Noises.SpaghettiRoughness));
        var spaghettiRoughnessModulator = DensityFunctions.MappedNoise(noises.GetValueOrThrow(Noises.SpaghettiRoughnessModulator), 0.0d, -0.1d);
        return DensityFunctions.CacheOnce(DensityFunctions.Mul(spaghettiRoughnessModulator, DensityFunctions.Add(spaghettiRoughnessNoise.Abs(), DensityFunctions.ConstantValue(-0.4d))));
    }

    //PreliminarySurfaceLevel preliminary surface level, maps to vanilla preliminarySurfaceLevel
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

    //YLimitedInterpolatable Y-limited interpolation, maps to vanilla yLimitedInterpolatable
    //Returns whenInRange when y falls in [minYInclusive, maxYInclusive], otherwise the constant whenOutOfRange
    private static DensityFunction YLimitedInterpolatable(DensityFunction y, DensityFunction whenInRange,
        int minYInclusive, int maxYInclusive, int whenOutOfRange)
        => DensityFunctions.Interpolated(
            DensityFunctions.RangeChoice(y, minYInclusive, maxYInclusive + 1, whenInRange, DensityFunctions.ConstantValue(whenOutOfRange)));

    //Remap linear remapping, maps to vanilla remap
    //Maps input from [fromMin, fromMax] to [toMin, toMax]
    private static DensityFunction Remap(DensityFunction input, double fromMin, double fromMax, double toMin, double toMax)
    {
        var factor = (toMax - toMin) / (fromMax - fromMin);
        var offset = toMin - (fromMin * factor);
        return DensityFunctions.Add(DensityFunctions.Mul(input, DensityFunctions.ConstantValue(factor)), DensityFunctions.ConstantValue(offset));
    }

    //QuantizedSpaghettiRarity quantized spaghetti rarity, maps to the vanilla QuantizedSpaghettiRarity nested class
    //Selects a noise function of different rarity by segments of the input value
    private static class QuantizedSpaghettiRarity
    {
        //WrapRarity2d 2D spaghetti rarity wrapper, maps to vanilla wrapRarity2d
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

        //WrapRarity3d 3D spaghetti rarity wrapper, maps to vanilla wrapRarity3d
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

        //NoiseFunctionForRarity rarity noise function, maps to vanilla noiseFunctionForRarity
        //rarity controls the reciprocal of frequency and the scale
        private static DensityFunction NoiseFunctionForRarity(NoiseParameters noise, double rarity)
            => DensityFunctions.Mul(DensityFunctions.ConstantValue(rarity), DensityFunctions.Noise(noise, 1.0 / rarity, 1.0 / rarity));
    }
}
