using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.Structures;

//PillarStructurePiece pillar piece, maps to a simplified vanilla StructurePiece subclass
//Provides the bounding box and NBT serialization for a single pillar; base point and height must round-trip
public sealed class PillarStructurePiece : StructurePiece
{
    public int Height { get; }

    public PillarStructurePiece(int baseX, int baseY, int baseZ, int height)
        : base(PillarPieceType.Instance, 0, new BoundingBoxInt(baseX, baseY, baseZ, baseX, baseY + height - 1, baseZ))
    {
        Height = height;
    }

    //FromTag restore a pillar from NBT; height comes from its own field, base point from the bounding box minimum
    public static PillarStructurePiece FromTag(CompoundTag tag)
    {
        var box = ReadBoundingBox(tag);
        return new PillarStructurePiece(box.MinX, box.MinY, box.MinZ, tag.GetIntOr("Height", 1));
    }

    //PostProcess placeholder for vanilla postProcess; real block placement to be wired up later
    public override void PostProcess(WorldGenRegion region, int chunkX, int chunkZ) { }

    //AddAdditionalSaveData write the height field so deserialization can restore it
    protected override void AddAdditionalSaveData(CompoundTag tag)
        => tag.PutInt("Height", Height);
}
