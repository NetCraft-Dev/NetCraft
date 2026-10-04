using NetCraft.Game.Util;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Util;

namespace NetCraft.Game.Data;

//TerrainProvider 地形样条构造器对应原版 net.minecraft.data.worldgen.TerrainProvider
//为 NoiseRouterData.overworld 提供 offset/factor/jaggedness 三套 CubicSpline
//Spline 树按 continents/erosion/ridges/weirdness 四维坐标分层决定地形高度系数
//amplified 模式通过 valueTransformer 放大 offset/factor/jaggedness 实现放大世界
public static class TerrainProvider
{
    private const float DeepOceanContinentalness = -0.51f;
    private const float OceanContinentalness = -0.4f;
    private const float PlainsContinentalness = 0.1f;
    private const float BeachContinentalness = -0.15f;

    //NoTransform 恒等变换对应原版 NO_TRANSFORM
    private static readonly Func<float, float> NoTransform = _ => _;

    //AmplifiedOffset 放大世界偏移变换对应原版 AMPLIFIED_OFFSET
    //负值保持(海平面以下不变)正值翻倍(陆地放大)
    private static readonly Func<float, float> AmplifiedOffset = offset => offset < 0.0f ? offset : offset * 2.0f;

    //AmplifiedFactor 放大世界系数变换对应原版 AMPLIFIED_FACTOR
    private static readonly Func<float, float> AmplifiedFactor = factor => 1.25f - (6.25f / (factor + 5.0f));

    //AmplifiedJaggedness 放大世界锯齿变换对应原版 AMPLIFIED_JAGGEDNESS
    private static readonly Func<float, float> AmplifiedJaggedness = jaggedness => jaggedness * 2.0f;

    //OverworldOffset 主世界偏移样条对应原版 overworldOffset
    //按 continents 分层选择 beach/low/mid/high 四套 erosion 子样条决定基础高度偏移
    public static CubicSpline OverworldOffset(DensityFunction continents, DensityFunction erosion, DensityFunction ridges, bool amplified)
    {
        var offsetTransformer = amplified ? AmplifiedOffset : NoTransform;
        var beachSpline = BuildErosionOffsetSpline(erosion, ridges, BeachContinentalness, 0.0f, 0.0f, 0.1f, 0.0f, -0.03f, false, false, offsetTransformer);
        var lowSpline = BuildErosionOffsetSpline(erosion, ridges, -0.1f, 0.03f, 0.1f, 0.1f, 0.01f, -0.03f, false, false, offsetTransformer);
        var midSpline = BuildErosionOffsetSpline(erosion, ridges, -0.1f, 0.03f, 0.1f, 0.7f, 0.01f, -0.03f, true, true, offsetTransformer);
        var highSpline = BuildErosionOffsetSpline(erosion, ridges, -0.05f, 0.03f, 0.1f, 1.0f, 0.01f, 0.01f, true, true, offsetTransformer);
        return CubicSpline.Builder(continents, offsetTransformer)
            .AddPoint(-1.1f, 0.044f)
            .AddPoint(-1.02f, -0.2222f)
            .AddPoint(DeepOceanContinentalness, -0.2222f)
            .AddPoint(-0.44f, -0.12f)
            .AddPoint(-0.18f, -0.12f)
            .AddPoint(-0.16f, beachSpline)
            .AddPoint(BeachContinentalness, beachSpline)
            .AddPoint(-0.1f, lowSpline)
            .AddPoint(0.25f, midSpline)
            .AddPoint(1.0f, highSpline)
            .Build();
    }

    //OverworldFactor 主世界系数样条对应原版 overworldFactor
    //按 continents 分层选择 beach/-0.1/0.03/0.06 四套 erosion factor 子样条决定高度缩放
    public static CubicSpline OverworldFactor(DensityFunction continents, DensityFunction erosion, DensityFunction weirdness, DensityFunction ridges, bool amplified)
    {
        var factorTransformer = amplified ? AmplifiedFactor : NoTransform;
        return CubicSpline.Builder(continents, NoTransform)
            .AddPoint(-0.19f, 3.95f)
            .AddPoint(BeachContinentalness, GetErosionFactor(erosion, weirdness, ridges, 6.25f, true, NoTransform))
            .AddPoint(-0.1f, GetErosionFactor(erosion, weirdness, ridges, 5.47f, true, factorTransformer))
            .AddPoint(0.03f, GetErosionFactor(erosion, weirdness, ridges, 5.08f, true, factorTransformer))
            .AddPoint(0.06f, GetErosionFactor(erosion, weirdness, ridges, 4.69f, false, factorTransformer))
            .Build();
    }

    //OverworldJaggedness 主世界锯齿样条对应原版 overworldJaggedness
    //按 continents 分层 -0.11/0.03/0.65 三段决定 jaggedness 噪声强度
    public static CubicSpline OverworldJaggedness(DensityFunction continents, DensityFunction erosion, DensityFunction weirdness, DensityFunction ridges, bool amplified)
    {
        var jaggednessTransformer = amplified ? AmplifiedJaggedness : NoTransform;
        return CubicSpline.Builder(continents, jaggednessTransformer)
            .AddPoint(-0.11f, 0.0f)
            .AddPoint(0.03f, BuildErosionJaggednessSpline(erosion, weirdness, ridges, 1.0f, 0.5f, 0.0f, 0.0f, jaggednessTransformer))
            .AddPoint(0.65f, BuildErosionJaggednessSpline(erosion, weirdness, ridges, 1.0f, 1.0f, 1.0f, 0.0f, jaggednessTransformer))
            .Build();
    }

    //BuildErosionJaggednessSpline 按 erosion 分四段选择 ridge jaggedness 子样条对应原版 buildErosionJaggednessSpline
    private static CubicSpline BuildErosionJaggednessSpline(DensityFunction erosion, DensityFunction weirdness, DensityFunction ridges,
        float jaggednessFactorAtPeakRidgeAndErosionIndex0, float jaggednessFactorAtPeakRidgeAndErosionIndex1,
        float jaggednessFactorAtHighRidgeAndErosionIndex0, float jaggednessFactorAtHighRidgeAndErosionIndex1,
        Func<float, float> jaggednessTransformer)
    {
        var ridgeJaggednessSplineAtErosion0 = BuildRidgeJaggednessSpline(weirdness, ridges, jaggednessFactorAtPeakRidgeAndErosionIndex0, jaggednessFactorAtHighRidgeAndErosionIndex0, jaggednessTransformer);
        var ridgeJaggednessSplineAtErosion1 = BuildRidgeJaggednessSpline(weirdness, ridges, jaggednessFactorAtPeakRidgeAndErosionIndex1, jaggednessFactorAtHighRidgeAndErosionIndex1, jaggednessTransformer);
        return CubicSpline.Builder(erosion, jaggednessTransformer)
            .AddPoint(-1.0f, ridgeJaggednessSplineAtErosion0)
            .AddPoint(-0.78f, ridgeJaggednessSplineAtErosion1)
            .AddPoint(-0.5775f, ridgeJaggednessSplineAtErosion1)
            .AddPoint(-0.375f, 0.0f)
            .Build();
    }

    //PeaksAndValleys 把 weirdness 转为山脊山谷值对应原版 peaksAndValleys
    //NoiseRouterData 用此把 ridges 转为 ridgesFolded
    public static float PeaksAndValleys(float weirdness)
        => (-Math.Abs(Math.Abs(weirdness) - 0.6666667f) + 0.33333334f) * 3.0f;

    //BuildRidgeJaggednessSpline 按 ridges 分段选择 weirdness jaggedness 子样条对应原版 buildRidgeJaggednessSpline
    private static CubicSpline BuildRidgeJaggednessSpline(DensityFunction weirdness, DensityFunction ridges,
        float jaggednessFactorAtPeakRidge, float jaggednessFactorAtHighRidge, Func<float, float> jaggednessTransformer)
    {
        var highSliceStart = PeaksAndValleys(0.4f);
        var highSliceEnd = PeaksAndValleys(0.56666666f);
        var highSliceMiddle = (highSliceStart + highSliceEnd) / 2.0f;
        var ridgeSpline = CubicSpline.Builder(ridges, jaggednessTransformer);
        ridgeSpline.AddPoint(highSliceStart, 0.0f);
        if (jaggednessFactorAtHighRidge > 0.0f)
            ridgeSpline.AddPoint(highSliceMiddle, BuildWeirdnessJaggednessSpline(weirdness, jaggednessFactorAtHighRidge, jaggednessTransformer));
        else
            ridgeSpline.AddPoint(highSliceMiddle, 0.0f);
        if (jaggednessFactorAtPeakRidge > 0.0f)
            ridgeSpline.AddPoint(1.0f, BuildWeirdnessJaggednessSpline(weirdness, jaggednessFactorAtPeakRidge, jaggednessTransformer));
        else
            ridgeSpline.AddPoint(1.0f, 0.0f);
        return ridgeSpline.Build();
    }

    //BuildWeirdnessJaggednessSpline 按 weirdness 分负正两段对应原版 buildWeirdnessJaggednessSpline
    private static CubicSpline BuildWeirdnessJaggednessSpline(DensityFunction weirdness, float jaggednessFactor, Func<float, float> jaggednessTransformer)
    {
        var maxJaggednessAtNegativeWeirdness = 0.63f * jaggednessFactor;
        var maxJaggednessAtPositiveWeirdness = 0.3f * jaggednessFactor;
        return CubicSpline.Builder(weirdness, jaggednessTransformer)
            .AddPoint(-0.01f, maxJaggednessAtNegativeWeirdness)
            .AddPoint(0.01f, maxJaggednessAtPositiveWeirdness)
            .Build();
    }

    //GetErosionFactor 按 erosion 分段选择 weirdness/ridges factor 子样条对应原版 getErosionFactor
    //shatteredTerrain=true 走破碎地形分支否则走极端山丘分支
    private static CubicSpline GetErosionFactor(DensityFunction erosion, DensityFunction weirdness, DensityFunction ridges,
        float baseValue, bool shatteredTerrain, Func<float, float> factorTransformer)
    {
        var baseSpline = CubicSpline.Builder(weirdness, factorTransformer)
            .AddPoint(-0.2f, 6.3f)
            .AddPoint(0.2f, baseValue)
            .Build();
        var erosionPoints = CubicSpline.Builder(erosion, factorTransformer)
            .AddPoint(-0.6f, baseSpline)
            .AddPoint(-0.5f, CubicSpline.Builder(weirdness, factorTransformer).AddPoint(-0.05f, 6.3f).AddPoint(0.05f, 2.67f).Build())
            .AddPoint(-0.35f, baseSpline)
            .AddPoint(-0.25f, baseSpline)
            .AddPoint(-0.1f, CubicSpline.Builder(weirdness, factorTransformer).AddPoint(-0.05f, 2.67f).AddPoint(0.05f, 6.3f).Build())
            .AddPoint(0.03f, baseSpline);
        if (shatteredTerrain)
        {
            var weirdnessShattered = CubicSpline.Builder(weirdness, factorTransformer).AddPoint(0.0f, baseValue).AddPoint(0.1f, 0.625f).Build();
            var ridgesShattered = CubicSpline.Builder(ridges, factorTransformer).AddPoint(-0.9f, baseValue).AddPoint(-0.69f, weirdnessShattered).Build();
            erosionPoints.AddPoint(0.35f, baseValue).AddPoint(0.45f, ridgesShattered).AddPoint(0.55f, ridgesShattered).AddPoint(0.62f, baseValue);
        }
        else
        {
            var extremeHillsTerrainFromMidSliceAndUp = CubicSpline.Builder(ridges, factorTransformer).AddPoint(-0.7f, baseSpline).AddPoint(BeachContinentalness, 1.37f).Build();
            var extra3dNoiseOnPeaksOnly = CubicSpline.Builder(ridges, factorTransformer).AddPoint(0.45f, baseSpline).AddPoint(0.7f, 1.56f).Build();
            erosionPoints.AddPoint(0.05f, extra3dNoiseOnPeaksOnly).AddPoint(0.4f, extra3dNoiseOnPeaksOnly).AddPoint(0.45f, extremeHillsTerrainFromMidSliceAndUp).AddPoint(0.55f, extremeHillsTerrainFromMidSliceAndUp).AddPoint(0.58f, baseValue);
        }
        return erosionPoints.Build();
    }

    //CalculateSlope 两点斜率对应原版 calculateSlope
    private static float CalculateSlope(float y1, float y2, float x1, float x2)
        => (y2 - y1) / (x2 - x1);

    //BuildMountainRidgeSplineWithPoints 山脊山地形样条对应原版 buildMountainRidgeSplineWithPoints
    //modulation 控制山高saddle 控制是否在 0 处加鞍点
    private static CubicSpline BuildMountainRidgeSplineWithPoints(DensityFunction ridges, float modulation, bool saddle, Func<float, float> offsetTransformer)
    {
        var build = CubicSpline.Builder(ridges, offsetTransformer);
        var minPointContinentalness = MountainContinentalness(-1.0f, modulation, -0.7f);
        var maxPointContinentalness = MountainContinentalness(1.0f, modulation, -0.7f);
        var ridgeZeroPoint = CalculateMountainRidgeZeroContinentalnessPoint(modulation);
        if (-0.65f < ridgeZeroPoint && ridgeZeroPoint < 1.0f)
        {
            var afterRiverThresholdContinentalness = MountainContinentalness(-0.65f, modulation, -0.7f);
            var beforeRiverThresholdContinentalness = MountainContinentalness(-0.75f, modulation, -0.7f);
            var minPointDerivative = CalculateSlope(minPointContinentalness, beforeRiverThresholdContinentalness, -1.0f, -0.75f);
            build.AddPoint(-1.0f, minPointContinentalness, minPointDerivative);
            build.AddPoint(-0.75f, beforeRiverThresholdContinentalness);
            build.AddPoint(-0.65f, afterRiverThresholdContinentalness);
            var ridgeZeroPointContinentalness = MountainContinentalness(ridgeZeroPoint, modulation, -0.7f);
            var maxPointDerivative = CalculateSlope(ridgeZeroPointContinentalness, maxPointContinentalness, ridgeZeroPoint, 1.0f);
            build.AddPoint(ridgeZeroPoint - 0.01f, ridgeZeroPointContinentalness);
            build.AddPoint(ridgeZeroPoint, ridgeZeroPointContinentalness, maxPointDerivative);
            build.AddPoint(1.0f, maxPointContinentalness, maxPointDerivative);
        }
        else
        {
            var simpleDerivative = CalculateSlope(minPointContinentalness, maxPointContinentalness, -1.0f, 1.0f);
            if (saddle)
            {
                build.AddPoint(-1.0f, Math.Max(0.2f, minPointContinentalness));
                build.AddPoint(0.0f, Mth.Lerp(0.5f, minPointContinentalness, maxPointContinentalness), simpleDerivative);
            }
            else
            {
                build.AddPoint(-1.0f, minPointContinentalness, simpleDerivative);
            }
            build.AddPoint(1.0f, maxPointContinentalness, simpleDerivative);
        }
        return build.Build();
    }

    //MountainContinentalness 山脊转大陆度对应原版 mountainContinentalness
    //ridge < allowRiversBelow 时下限 -0.2222否则下限 0
    private static float MountainContinentalness(float ridge, float modulation, float allowRiversBelow)
    {
        var ridgeSlope = 1.0f - ((1.0f - modulation) * 0.5f);
        var ridgeIntersect = 0.5f * (1.0f - modulation);
        var adjustedRidgeHeight = (ridge + 1.17f) * 0.46082947f;
        var continentalness = (adjustedRidgeHeight * ridgeSlope) - ridgeIntersect;
        if (ridge < allowRiversBelow)
            return Math.Max(continentalness, -0.2222f);
        return Math.Max(continentalness, 0.0f);
    }

    //CalculateMountainRidgeZeroContinentalnessPoint 计算大陆度为零的山脊点对应原版 calculateMountainRidgeZeroContinentalnessPoint
    private static float CalculateMountainRidgeZeroContinentalnessPoint(float modulation)
    {
        var ridgeSlope = 1.0f - ((1.0f - modulation) * 0.5f);
        var ridgeIntersect = 0.5f * (1.0f - modulation);
        return (ridgeIntersect / (0.46082947f * ridgeSlope)) - 1.17f;
    }

    //BuildErosionOffsetSpline 侵蚀偏移样条对应原版 buildErosionOffsetSpline
    //按 erosion 分段选择 mountains/plateau/plains/swamps 等子样条
    public static CubicSpline BuildErosionOffsetSpline(DensityFunction erosion, DensityFunction ridges,
        float lowValley, float hill, float tallHill, float mountainFactor, float plain, float swamp,
        bool includeExtremeHills, bool saddle, Func<float, float> offsetTransformer)
    {
        var veryLowErosionMountains = BuildMountainRidgeSplineWithPoints(ridges, Mth.Lerp(mountainFactor, 0.6f, 1.5f), saddle, offsetTransformer);
        var lowErosionMountains = BuildMountainRidgeSplineWithPoints(ridges, Mth.Lerp(mountainFactor, 0.6f, 1.0f), saddle, offsetTransformer);
        var mountains = BuildMountainRidgeSplineWithPoints(ridges, mountainFactor, saddle, offsetTransformer);
        var widePlateau = RidgeSpline(ridges, lowValley - 0.15f, 0.5f * mountainFactor, Mth.Lerp(0.5f, 0.5f, 0.5f) * mountainFactor, 0.5f * mountainFactor, 0.6f * mountainFactor, 0.5f, offsetTransformer);
        var narrowPlateau = RidgeSpline(ridges, lowValley, plain * mountainFactor, hill * mountainFactor, 0.5f * mountainFactor, 0.6f * mountainFactor, 0.5f, offsetTransformer);
        var plains = RidgeSpline(ridges, lowValley, plain, plain, hill, tallHill, 0.5f, offsetTransformer);
        var plainsFarInland = RidgeSpline(ridges, lowValley, plain, plain, hill, tallHill, 0.5f, offsetTransformer);
        var extremeHills = CubicSpline.Builder(ridges, offsetTransformer).AddPoint(-1.0f, lowValley).AddPoint(OceanContinentalness, plains).AddPoint(0.0f, tallHill + 0.07f).Build();
        var swamps = RidgeSpline(ridges, -0.02f, swamp, swamp, hill, tallHill, 0.0f, offsetTransformer);
        var builder = CubicSpline.Builder(erosion, offsetTransformer)
            .AddPoint(-0.85f, veryLowErosionMountains)
            .AddPoint(-0.7f, lowErosionMountains)
            .AddPoint(OceanContinentalness, mountains)
            .AddPoint(-0.35f, widePlateau)
            .AddPoint(-0.1f, narrowPlateau)
            .AddPoint(0.2f, plains);
        if (includeExtremeHills)
        {
            builder.AddPoint(0.4f, plainsFarInland).AddPoint(0.45f, extremeHills).AddPoint(0.55f, extremeHills).AddPoint(0.58f, plainsFarInland);
        }
        builder.AddPoint(0.7f, swamps);
        return builder.Build();
    }

    //RidgeSpline 山脊样条对应原版 ridgeSpline
    //按 ridges -1/OCEAN/0/0.4/1 五点决定 valley/low/mid/high/peaks
    private static CubicSpline RidgeSpline(DensityFunction ridges, float valley, float low, float mid, float high, float peaks, float minValleySteepness, Func<float, float> offsetTransformer)
    {
        var d1 = Math.Max(0.5f * (low - valley), minValleySteepness);
        var d2 = 5.0f * (mid - low);
        return CubicSpline.Builder(ridges, offsetTransformer)
            .AddPoint(-1.0f, valley, d1)
            .AddPoint(OceanContinentalness, low, Math.Min(d1, d2))
            .AddPoint(0.0f, mid, d2)
            .AddPoint(0.4f, high, 2.0f * (high - mid))
            .AddPoint(1.0f, peaks, 0.7f * (peaks - high))
            .Build();
    }
}
