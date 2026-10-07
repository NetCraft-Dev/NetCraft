using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Game.World.Level.LevelGen.Structure;
//LevelGen has a sibling namespace also named Structure, so use an alias to disambiguate
using StructureBase = NetCraft.Game.World.Level.LevelGen.Structure.Structure;

namespace NetCraft.Game.World.Level.LevelGen.Structures;

//PillarStructureFeature single-pillar sample structure, maps to the simplest vanilla Structure subclass
//Used to validate the STRUCTURE_START assembly path; not part of any structure set so it never generates naturally
public sealed class PillarStructureFeature : StructureBase
{
    //PillarType pillar type, registered under the same name as the structure
    public static readonly StructureType PillarType = new(Identifier.WithDefaultNamespace("pillar"));

    public const int PillarHeight = 16;
    public const int BaseY = 64;

    public override Identifier Id => Identifier.WithDefaultNamespace("pillar");

    public override StructureType Type => PillarType;

    //Empty biome list means no biome passes the filter; the sample structure never generates naturally
    public PillarStructureFeature() : base(StructureGenerationSettings.Default) { }

    //FindGenerationPoint generate a single pillar at the chunk center
    public override GenerationStub? FindGenerationPoint(GenerationContext context)
    {
        var chunkPos = context.ChunkPos;
        var baseX = chunkPos.X * 16 + 8;
        var baseZ = chunkPos.Z * 16 + 8;
        var piece = new PillarStructurePiece(baseX, BaseY, baseZ, PillarHeight);
        return new GenerationStub(new BlockPos(baseX, BaseY, baseZ),
            new StructurePiece[] { piece });
    }
}
