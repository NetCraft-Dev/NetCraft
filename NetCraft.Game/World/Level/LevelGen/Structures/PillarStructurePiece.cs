using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.Structures;

//PillarStructurePiece 石柱部件对应原版简化 StructurePiece 子类
//提供单根石柱的包围盒与 NBT 序列化 底点与高度都要能读回来
public sealed class PillarStructurePiece : StructurePiece
{
    public int Height { get; }

    public PillarStructurePiece(int baseX, int baseY, int baseZ, int height)
        : base(PillarPieceType.Instance, 0, new BoundingBoxInt(baseX, baseY, baseZ, baseX, baseY + height - 1, baseZ))
    {
        Height = height;
    }

    //FromTag 从 NBT 还原石柱 高度来自自身字段 底点在包围盒最小值
    public static PillarStructurePiece FromTag(CompoundTag tag)
    {
        var box = ReadBoundingBox(tag);
        return new PillarStructurePiece(box.MinX, box.MinY, box.MinZ, tag.GetIntOr("Height", 1));
    }

    //PostProcess 占位对应原版 postProcess 真实方块写入待后续接入
    public override void PostProcess(WorldGenRegion region, int chunkX, int chunkZ) { }

    //AddAdditionalSaveData 写入高度字段供反序列化还原
    protected override void AddAdditionalSaveData(CompoundTag tag)
        => tag.PutInt("Height", Height);
}
