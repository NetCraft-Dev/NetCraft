using NetCraft.Game.World.Level.LevelGen.Features;
using NetCraft.Primitives;
using NetCraft.Util;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//Beardifier 结构地形适配核 对应原版 net.minecraft.world.level.levelgen.Beardifier
//噪声密度里加的一项 让村庄这类结构把地面整平 接缝处平滑过渡
//片段与接缝按距离对密度做加权贡献 影响范围之外的坐标直接返回 0
public sealed class Beardifier : SimpleFunction
{
    //BeardKernelRadius 核函数半径 原版 12 格
    public const int BeardKernelRadius = 12;

    private const int BeardKernelSize = 24;
    private const int BeardKernelArea = BeardKernelSize * BeardKernelSize;

    //BeardKernel 预计算的核函数查表 下标是 [zi][xi][yi]
    //逐方块现算要开三次方与指数 预计算一次全程复用
    private static readonly float[] BeardKernel = CreateBeardKernel();

    //Empty 无结构影响时的空实例 不占影响范围恒返回 0
    public static readonly Beardifier Empty = new(new List<Rigid>(), new List<JigsawJunction>(), null);

    private readonly IReadOnlyList<Rigid> _pieces;
    private readonly IReadOnlyList<JigsawJunction> _junctions;
    private readonly BoundingBoxInt? _affectedBox;

    public Beardifier(IReadOnlyList<Rigid> pieces, IReadOnlyList<JigsawJunction> junctions,
        BoundingBoxInt? affectedBox)
    {
        _pieces = pieces;
        _junctions = junctions;
        _affectedBox = affectedBox;
    }

    //ForStructuresInChunk 收集影响该区块的结构片段与接缝 对应原版 forStructuresInChunk
    //只要需要改编地形的结构 片段按 12 格判定是否够近 影响范围是全体相关包围盒外扩 24 格
    public static Beardifier ForStructuresInChunk(StructureFeatureManager manager, ChunkPos chunkPos)
    {
        var starts = manager.StartsForStructure(chunkPos, start =>
            start.Structure is Structure structure
            && structure.Settings.TerrainAdaptation != TerrainAdjustment.None);
        if (starts.Count == 0) return Empty;

        var chunkBlockX = chunkPos.X << 4;
        var chunkBlockZ = chunkPos.Z << 4;
        var rigids = new List<Rigid>();
        var junctions = new List<JigsawJunction>();
        BoundingBoxInt? anyBox = null;
        foreach (var start in starts)
        {
            if (start.Structure is not Structure structure) continue;
            var terrainAdjustment = structure.Settings.TerrainAdaptation;
            foreach (var piece in start.Pieces)
            {
                if (!piece.IsCloseToChunk(chunkPos, BeardKernelRadius)) continue;
                if (piece is PoolElementStructurePiece poolPiece)
                {
                    //只有刚性投影的元素才把地形顶起来 地形匹配投影跟着地面走
                    if (poolPiece.Element.Projection == StructureTemplatePool.Projection.Rigid)
                    {
                        rigids.Add(new Rigid(poolPiece.BoundingBox, terrainAdjustment, poolPiece.GroundLevelDelta));
                        anyBox = IncludeBoundingBox(anyBox, piece.BoundingBox);
                    }
                    foreach (var junction in poolPiece.Junctions)
                    {
                        var junctionX = junction.SourceX;
                        var junctionZ = junction.SourceZ;
                        if (junctionX <= chunkBlockX - BeardKernelRadius) continue;
                        if (junctionZ <= chunkBlockZ - BeardKernelRadius) continue;
                        if (junctionX >= chunkBlockX + 15 + BeardKernelRadius) continue;
                        if (junctionZ >= chunkBlockZ + 15 + BeardKernelRadius) continue;
                        junctions.Add(junction);
                        var groundY = junction.SourceGroundY;
                        anyBox = IncludeBoundingBox(anyBox,
                            new BoundingBoxInt(junctionX, groundY, junctionZ, junctionX, groundY, junctionZ));
                    }
                }
                else
                {
                    rigids.Add(new Rigid(piece.BoundingBox, terrainAdjustment, 0));
                    anyBox = IncludeBoundingBox(anyBox, piece.BoundingBox);
                }
            }
        }
        if (anyBox is null) return Empty;
        return new Beardifier(rigids, junctions, anyBox.InflatedBy(BeardKernelSize));
    }

    private static BoundingBoxInt IncludeBoundingBox(BoundingBoxInt? encompassing, BoundingBoxInt box)
        => encompassing is null ? box : encompassing.Encapsulate(box);

    //FillArray 影响范围为空时整批填 0 对应原版 fillArray
    //有影响范围时交给逐点采样 核函数随坐标变化不能整批填充
    void DensityFunction.FillArray(double[] output, ContextProvider contextProvider)
    {
        if (_affectedBox is null)
        {
            Array.Fill(output, 0.0);
            return;
        }
        contextProvider.FillAllDirectly(output, this);
    }

    //Compute 累加各片段与接缝的贡献 对应原版 compute
    //四种适配方式取的纵向距离不同 贡献系数也不同
    public double Compute(FunctionContext context)
    {
        if (_affectedBox is not BoundingBoxInt affectedBox) return 0.0;
        var blockX = context.BlockX;
        var blockY = context.BlockY;
        var blockZ = context.BlockZ;
        if (!affectedBox.IsInside(blockX, blockY, blockZ)) return 0.0;

        var noiseValue = 0.0;
        foreach (var rigid in _pieces)
        {
            var box = rigid.Box;
            var groundLevelDelta = rigid.GroundLevelDelta;
            var dx = Math.Max(0, Math.Max(box.MinX - blockX, blockX - box.MaxX));
            var dz = Math.Max(0, Math.Max(box.MinZ - blockZ, blockZ - box.MaxZ));
            var groundY = box.MinY + groundLevelDelta;
            var dyToGround = blockY - groundY;
            var dy = rigid.TerrainAdjustment switch
            {
                TerrainAdjustment.None => 0,
                //掩埋与薄胡须都用"离结构地面的高度"
                TerrainAdjustment.Bury or TerrainAdjustment.BeardThin => dyToGround,
                TerrainAdjustment.BeardBox => Math.Max(0, Math.Max(groundY - blockY, blockY - box.MaxY)),
                TerrainAdjustment.Encapsulate => Math.Max(0, Math.Max(box.MinY - blockY, blockY - box.MaxY)),
                _ => 0,
            };
            noiseValue += rigid.TerrainAdjustment switch
            {
                TerrainAdjustment.None => 0.0,
                TerrainAdjustment.Bury => GetBuryContribution(dx, dy / 2.0, dz),
                TerrainAdjustment.BeardThin or TerrainAdjustment.BeardBox =>
                    GetBeardContribution(dx, dy, dz, dyToGround) * 0.8,
                TerrainAdjustment.Encapsulate => GetBuryContribution(dx / 2.0, dy / 2.0, dz / 2.0) * 0.8,
                _ => 0.0,
            };
        }
        foreach (var junction in _junctions)
        {
            var junctionDx = blockX - junction.SourceX;
            var junctionDy = blockY - junction.SourceGroundY;
            noiseValue += GetBeardContribution(junctionDx, junctionDy, blockZ - junction.SourceZ, junctionDy) * 0.4;
        }
        return noiseValue;
    }

    public double MinValue => double.NegativeInfinity;

    public double MaxValue => double.PositiveInfinity;

    //GetBuryContribution 掩埋贡献 六格之内线性衰减到 0 对应原版 getBuryContribution
    private static double GetBuryContribution(double dx, double dy, double dz)
    {
        var distance = Mth.Length(dx, dy, dz);
        return Mth.ClampedMap(distance, 0.0, 6.0, 1.0, 0.0);
    }

    //GetBeardContribution 胡须贡献 查预计算核并按距离归一 对应原版 getBeardContribution
    private static double GetBeardContribution(int dx, int dy, int dz, int yToGround)
    {
        var xi = dx + BeardKernelRadius;
        var yi = dy + BeardKernelRadius;
        var zi = dz + BeardKernelRadius;
        if (!IsInKernelRange(xi) || !IsInKernelRange(yi) || !IsInKernelRange(zi)) return 0.0;
        var dyWithOffset = yToGround + 0.5;
        var distanceSqr = Mth.LengthSquared(dx, dyWithOffset, dz);
        var value = -dyWithOffset * Mth.FastInvSqrt(distanceSqr / 2.0) / 2.0;
        return value * BeardKernel[zi * BeardKernelArea + xi * BeardKernelSize + yi];
    }

    private static bool IsInKernelRange(int value) => value >= 0 && value < BeardKernelSize;

    //CreateBeardKernel 预计算 24 立方核 对应原版 BEARD_KERNEL 的静态初始化
    private static float[] CreateBeardKernel()
    {
        var kernel = new float[BeardKernelArea * BeardKernelSize];
        for (var zi = 0; zi < BeardKernelSize; zi++)
        for (var xi = 0; xi < BeardKernelSize; xi++)
        for (var yi = 0; yi < BeardKernelSize; yi++)
            kernel[zi * BeardKernelArea + xi * BeardKernelSize + yi] =
                (float)ComputeBeardContribution(xi - BeardKernelRadius, yi - BeardKernelRadius,
                    zi - BeardKernelRadius);
        return kernel;
    }

    private static double ComputeBeardContribution(int dx, int dy, int dz)
        => ComputeBeardContribution(dx, dy + 0.5, dz);

    private static double ComputeBeardContribution(int dx, double dy, int dz)
    {
        var distanceSqr = Mth.LengthSquared(dx, dy, dz);
        return Math.Pow(Math.E, -distanceSqr / 16.0);
    }

    //Rigid 一个刚性片段的地形贡献参数 对应原版 Beardifier.Rigid
    public sealed record Rigid(BoundingBoxInt Box, TerrainAdjustment TerrainAdjustment, int GroundLevelDelta);
}
