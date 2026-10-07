using NetCraft.Game.World.Level.LevelGen.Features;
using NetCraft.Primitives;
using NetCraft.Util;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//Beardifier structure terrain adaptation core, maps to vanilla net.minecraft.world.level.levelgen.Beardifier
//A term added into the noise density that lets structures like villages flatten the ground and blend smoothly at seams
//Pieces and junctions contribute weighted density by distance; coordinates outside the affected range return 0
public sealed class Beardifier : SimpleFunction
{
    //BeardKernelRadius kernel radius, 12 blocks in vanilla
    public const int BeardKernelRadius = 12;

    private const int BeardKernelSize = 24;
    private const int BeardKernelArea = BeardKernelSize * BeardKernelSize;

    //BeardKernel precomputed kernel lookup table indexed by [zi][xi][yi]
    //Computing per block needs cbrt and exp; precompute once and reuse everywhere
    private static readonly float[] BeardKernel = CreateBeardKernel();

    //Empty empty instance when no structure has influence; occupies no affected range and always returns 0
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

    //ForStructuresInChunk collects structure pieces and junctions affecting the chunk, maps to vanilla forStructuresInChunk
    //Only structures that need terrain adaptation; pieces are checked within 12 blocks, affected range is the union of relevant boxes inflated by 24 blocks
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
                    //Only rigid-projection elements raise the terrain; terrain-matching projection follows the ground
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

    //FillArray fills the whole batch with 0 when the affected range is empty, maps to vanilla fillArray
    //With an affected range, fall back to per-point sampling since the kernel varies by coordinate
    void DensityFunction.FillArray(double[] output, ContextProvider contextProvider)
    {
        if (_affectedBox is null)
        {
            Array.Fill(output, 0.0);
            return;
        }
        contextProvider.FillAllDirectly(output, this);
    }

    //Compute accumulates the contributions of all pieces and junctions, maps to vanilla compute
    //The four adaptation modes use different vertical distances and contribution factors
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
                //Bury and beard-thin both use "height above the structure ground"
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

    //GetBuryContribution bury contribution, decays linearly to 0 within six blocks, maps to vanilla getBuryContribution
    private static double GetBuryContribution(double dx, double dy, double dz)
    {
        var distance = Mth.Length(dx, dy, dz);
        return Mth.ClampedMap(distance, 0.0, 6.0, 1.0, 0.0);
    }

    //GetBeardContribution beard contribution, looks up the precomputed kernel and normalizes by distance, maps to vanilla getBeardContribution
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

    //CreateBeardKernel precomputes the 24-cube kernel, maps to the static init of vanilla BEARD_KERNEL
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

    //Rigid terrain contribution params of one rigid piece, maps to vanilla Beardifier.Rigid
    public sealed record Rigid(BoundingBoxInt Box, TerrainAdjustment TerrainAdjustment, int GroundLevelDelta);
}
