using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureStart structure assembly result, maps to vanilla StructureStart
//Holds the structure reference and piece list; validity depends only on whether pieces exist
//The bounding box envelopes all pieces; terrain adaptation inflates it further by terrain_adaptation
public sealed class StructureStart
{
    //InvalidStartId invalid marker, maps to vanilla INVALID_START_ID
    public const string InvalidStartId = "invalid";

    //Invalid invalid result returned instead of null on generation failure; the caller checks IsValid
    public static readonly StructureStart Invalid = new();

    //Structure structure reference, null when invalid
    public NetCraft.Registry.Structure? Structure { get; }

    //StructureId structure registry name, "invalid" when invalid
    public Identifier StructureId => Structure is Structure structure
        ? structure.Id
        : Identifier.WithDefaultNamespace(InvalidStartId);

    public ChunkPos ChunkPos { get; }

    //Pieces piece list; an empty list means invalid
    public IReadOnlyList<StructurePiece> Pieces { get; }

    //BoundingBox envelope of all pieces; a zero box when there are none
    public BoundingBoxInt BoundingBox { get; }

    //IsValid validity check, maps to vanilla isValid; only looks at whether pieces exist
    public bool IsValid => Pieces.Count > 0;

    //References how many times adjacent chunks have referenced this, maps to vanilla references
    public int References { get; private set; }

    //MaxReferences reference cap; past it adjacent chunks stop reusing this assembly result
    public const int MaxReferences = 1;

    //CanBeReferenced whether it can still be referenced, maps to vanilla canBeReferenced
    public bool CanBeReferenced => References < MaxReferences;

    //AddReference increments the reference count, maps to vanilla addReference
    public void AddReference() => References++;

    //CreateTag serializes the assembly result, maps to vanilla StructureStart.createTag
    //Field names and value styles match vanilla verbatim; each piece writes its own fields via WriteSaveData
    public CompoundTag CreateTag()
    {
        var tag = new CompoundTag();
        if (!IsValid)
        {
            //An invalid assembly writes only a marker; it reads back as invalid and does not pollute the chunk's structure table
            tag.PutString("id", InvalidStartId);
            return tag;
        }
        tag.PutString("id", StructureId.ToString());
        tag.PutInt("ChunkX", ChunkPos.X);
        tag.PutInt("ChunkZ", ChunkPos.Z);
        tag.PutInt("references", References);
        var children = new ListTag();
        foreach (var piece in Pieces) children.Add(piece.WriteSaveData());
        tag.Put("Children", children);
        return tag;
    }

    //LoadStaticStart restores the assembly result from NBT, maps to vanilla loadStaticStart
    //Returns null when the structure registry name is unknown, skipped by the caller; an unknown piece type skips just that piece
    public static StructureStart? LoadStaticStart(StructurePieceSerializationContext context, CompoundTag tag)
    {
        var idText = tag.GetStringValue("id");
        if (idText.Length == 0) return null;
        if (idText == InvalidStartId) return Invalid;
        var structureId = Identifier.TryParse(idText);
        if (structureId is null) return null;
        var structure = BuiltInRegistries.STRUCTURE.GetValue(structureId.Value);
        if (structure is null) return null;

        var chunkPos = new ChunkPos(tag.GetIntOr("ChunkX", 0), tag.GetIntOr("ChunkZ", 0));
        var pieces = new List<StructurePiece>();
        var children = tag.GetList("Children");
        if (children is not null)
        {
            foreach (var entry in children)
            {
                if (entry is not CompoundTag pieceTag) continue;
                var pieceIdText = pieceTag.GetStringValue("id");
                var pieceId = Identifier.TryParse(pieceIdText);
                if (pieceId is null) continue;
                //An unknown piece type is skipped as in vanilla; keeping the other pieces beats failing the whole assembly
                if (BuiltInRegistries.STRUCTURE_PIECE.GetValue(pieceId.Value) is not StructurePieceType pieceType)
                    continue;
                pieces.Add(pieceType.Load(context, pieceTag));
            }
        }

        var start = new StructureStart(structure, chunkPos, pieces) { References = tag.GetIntOr("references", 0) };
        return start;
    }

    private StructureStart()
    {
        Structure = null;
        ChunkPos = new ChunkPos(0, 0);
        Pieces = Array.Empty<StructurePiece>();
        BoundingBox = new BoundingBoxInt(0, 0, 0, 0, 0, 0);
    }

    public StructureStart(NetCraft.Registry.Structure structure, ChunkPos chunkPos,
        IReadOnlyList<StructurePiece> pieces)
    {
        Structure = structure;
        ChunkPos = chunkPos;
        Pieces = pieces;
        var box = pieces.Count == 0 ? new BoundingBoxInt(0, 0, 0, 0, 0, 0) : pieces[0].BoundingBox;
        for (var i = 1; i < pieces.Count; i++) box = box.Encapsulate(pieces[i].BoundingBox);
        BoundingBox = box;
    }

    //AdjustBoundingBox inflates the bounding box per terrain adaptation, maps to vanilla Structure.adjustBoundingBox
    //Inflates by 12 blocks when terrain adaptation is needed; the noise stage's terrain kernel uses it to compute the affected range
    public BoundingBoxInt AdjustBoundingBox()
        => Structure is Structure structure
            ? BoundingBox.InflatedBy(structure.Settings.TerrainAdaptation.BeardEdgeNeeded())
            : BoundingBox;

    //PlaceInChunk writes the pieces falling inside the target chunk into the world, maps to vanilla StructureStart.placeInChunk
    //Only pieces intersecting the target chunk are handled; out-of-bounds writes are silently dropped by WorldGenRegion per the write radius
    //Each decoration step runs structures then features; a structure must land first for features to grow on it
    public void PlaceInChunk(WorldGenRegion region, int chunkX, int chunkZ)
    {
        var chunkBox = BoundingBoxInt.FromChunkPos(new ChunkPos(chunkX, chunkZ));
        foreach (var piece in Pieces)
        {
            if (!piece.BoundingBox.Intersects(chunkBox)) continue;
            piece.PostProcess(region, chunkX, chunkZ);
        }
    }
}
