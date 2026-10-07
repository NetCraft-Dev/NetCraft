using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
//Both the Registry layer and the Game layer have a StructurePieceType name; the Game layer is the real type carrying restoration logic
using GamePieceType = NetCraft.Game.World.Level.LevelGen.Structure.StructurePieceType;

namespace NetCraft.Game.World.Level.LevelGen;

//StructurePiece abstract base class for structure pieces, maps to vanilla net.minecraft.world.level.levelgen.structure.StructurePiece
//Holds the piece type, generation depth and bounding box; subclasses implement the piece generation and their own NBT fields
public abstract class StructurePiece
{
    //PieceType piece type, written as id on save and used to dispatch back to the concrete subclass on load
    public GamePieceType? PieceType { get; }

    //GenDepth generation depth, the jigsaw recursion level; vanilla uses it to cap the depth
    public int GenDepth { get; }

    public BoundingBoxInt BoundingBox { get; protected set; }

    protected StructurePiece(BoundingBoxInt boundingBox)
        : this(null, 0, boundingBox) { }

    protected StructurePiece(GamePieceType? pieceType, int genDepth, BoundingBoxInt boundingBox)
    {
        PieceType = pieceType;
        GenDepth = genDepth;
        BoundingBox = boundingBox;
    }

    //Restores the common fields from NBT, maps to the vanilla StructurePiece(type, tag) constructor
    //The BB/O/GD fields are consumed by the base class; the subclass constructor then reads its own fields
    protected StructurePiece(GamePieceType? pieceType, CompoundTag tag)
    {
        PieceType = pieceType;
        GenDepth = tag.GetIntOr("GD", 0);
        BoundingBox = ReadBoundingBox(tag);
    }

    //Move translates the whole bounding box, maps to vanilla StructurePiece.move
    //Subclasses that hold their own position must override it to move the position too
    public virtual void Move(int dx, int dy, int dz)
        => BoundingBox = new BoundingBoxInt(
            BoundingBox.MinX + dx, BoundingBox.MinY + dy, BoundingBox.MinZ + dz,
            BoundingBox.MaxX + dx, BoundingBox.MaxY + dy, BoundingBox.MaxZ + dz);

    //PostProcess post-processing, maps to vanilla postProcess
    //Called per chunk by StructureStart.PlaceInChunk during decoration; subclasses override to write real blocks
    //region limits the write radius and out-of-range writes are dropped silently
    public virtual void PostProcess(WorldGenRegion region, int chunkX, int chunkZ) { }

    //IsCloseToChunk whether the piece falls within distance blocks of the target chunk, maps to vanilla isCloseToChunk
    //Terrain adaptation checks at 12 blocks whether a piece affects the current chunk
    public bool IsCloseToChunk(ChunkPos pos, int distance)
    {
        var minX = pos.X << 4;
        var minZ = pos.Z << 4;
        return BoundingBox.MaxX >= minX - distance && BoundingBox.MinX <= minX + 15 + distance
            && BoundingBox.MaxZ >= minZ - distance && BoundingBox.MinZ <= minZ + 15 + distance;
    }

    //AddAdditionalSaveData writes extra data into the CompoundTag, maps to vanilla addAdditionalSaveData
    //Empty by default; subclasses override to persist custom fields
    protected virtual void AddAdditionalSaveData(CompoundTag tag) { }

    //WriteSaveData serialises the piece into a CompoundTag, maps to vanilla StructurePiece.createTag
    //Field names and value forms match vanilla word for word; id is the type registry name and BB is a six-int bounding box
    public CompoundTag WriteSaveData()
    {
        var tag = new CompoundTag();
        tag.PutString("id", (PieceType?.Id ?? Identifier.WithDefaultNamespace("invalid")).ToString());
        tag.PutIntArray("BB", new[]
        {
            BoundingBox.MinX, BoundingBox.MinY, BoundingBox.MinZ,
            BoundingBox.MaxX, BoundingBox.MaxY, BoundingBox.MaxZ,
        });
        //No carrier for facing is implemented, so -1 is always written, matching vanilla's "no facing" value
        tag.PutInt("O", -1);
        tag.PutInt("GD", GenDepth);
        AddAdditionalSaveData(tag);
        return tag;
    }

    //ReadBoundingBox restores the bounding box from the BB int array, maps to the int array form of vanilla BoundingBox.CODEC
    protected static BoundingBoxInt ReadBoundingBox(CompoundTag tag)
    {
        var array = tag.GetIntArray("BB")?.Value;
        if (array is null || array.Length < 6) return new BoundingBoxInt(0, 0, 0, 0, 0, 0);
        return new BoundingBoxInt(array[0], array[1], array[2], array[3], array[4], array[5]);
    }
}

//BoundingBoxInt integer bounding box, maps to vanilla net.minecraft.world.level.levelgen.structure.BoundingBox
//Simplified to an int 6-tuple holding minX/maxX/minY/maxY/minZ/maxZ
public sealed record BoundingBoxInt(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    //FromChunkPos builds a 16x16x384 bounding box from a ChunkPos, maps to the vanilla chunk region
    public static BoundingBoxInt FromChunkPos(ChunkPos pos, int minY = -64, int maxY = 320)
        => new(pos.X << 4, minY, pos.Z << 4, (pos.X << 4) + 15, maxY, (pos.Z << 4) + 15);

    public int LengthX => MaxX - MinX + 1;
    public int LengthY => MaxY - MinY + 1;
    public int LengthZ => MaxZ - MinZ + 1;

    //IsInside whether a coordinate is inside the box, maps to vanilla BoundingBox.isInside
    //The terrain adaptation kernel only evaluates coordinates in range
    public bool IsInside(int x, int y, int z)
        => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;

    //Intersects tests whether two bounding boxes intersect, maps to vanilla BoundingBox.intersects
    public bool Intersects(BoundingBoxInt other)
        => MaxX >= other.MinX && MinX <= other.MaxX
        && MaxY >= other.MinY && MinY <= other.MaxY
        && MaxZ >= other.MinZ && MinZ <= other.MaxZ;

    //Encapsulate merges two bounding boxes, maps to vanilla BoundingBox.encapsulate
    public BoundingBoxInt Encapsulate(BoundingBoxInt other)
        => new(
            Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Min(MinZ, other.MinZ),
            Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY), Math.Max(MaxZ, other.MaxZ));

    //InflatedBy equal expansion on all six faces, maps to vanilla BoundingBox.inflatedBy
    //Terrain adaptation uses it to expand by 12 blocks per terrain_adaptation
    public BoundingBoxInt InflatedBy(int amount)
        => new(MinX - amount, MinY - amount, MinZ - amount, MaxX + amount, MaxY + amount, MaxZ + amount);
}
