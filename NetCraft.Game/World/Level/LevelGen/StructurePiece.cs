using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
//Registry 层与 Game 层都有 StructurePieceType 名字 Game 层才是带还原逻辑的实际类型
using GamePieceType = NetCraft.Game.World.Level.LevelGen.Structure.StructurePieceType;

namespace NetCraft.Game.World.Level.LevelGen;

//StructurePiece 结构部件抽象基类对应原版 net.minecraft.world.level.levelgen.structure.StructurePiece
//持片段类型 生成深度与包围盒 子类实现具体部件生成逻辑与自身字段的 NBT 读写
public abstract class StructurePiece
{
    //PieceType 片段类型 落盘写成 id 读档时靠它派发回具体子类
    public GamePieceType? PieceType { get; }

    //GenDepth 生成深度 拼图递归层级原版用它限制层数
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

    //从 NBT 还原通用字段 对应原版 StructurePiece(type, tag) 构造
    //BB/O/GD 三个字段由基类消费 子类构造再读自己的字段
    protected StructurePiece(GamePieceType? pieceType, CompoundTag tag)
    {
        PieceType = pieceType;
        GenDepth = tag.GetIntOr("GD", 0);
        BoundingBox = ReadBoundingBox(tag);
    }

    //Move 整体平移边界框对应原版 StructurePiece.move
    //持自身位置的子类要覆盖它把位置一起挪动
    public virtual void Move(int dx, int dy, int dz)
        => BoundingBox = new BoundingBoxInt(
            BoundingBox.MinX + dx, BoundingBox.MinY + dy, BoundingBox.MinZ + dz,
            BoundingBox.MaxX + dx, BoundingBox.MaxY + dy, BoundingBox.MaxZ + dz);

    //PostProcess 后处理对应原版 postProcess
    //装饰阶段由 StructureStart.PlaceInChunk 逐区块调用 子类覆盖提供真实方块写入
    //region 限定了写入半径 越界写入会被静默丢弃
    public virtual void PostProcess(WorldGenRegion region, int chunkX, int chunkZ) { }

    //IsCloseToChunk 片段是否落在目标区块扩展 distance 格之内 对应原版 isCloseToChunk
    //地形适配按 12 格判定片段是否影响当前区块
    public bool IsCloseToChunk(ChunkPos pos, int distance)
    {
        var minX = pos.X << 4;
        var minZ = pos.Z << 4;
        return BoundingBox.MaxX >= minX - distance && BoundingBox.MinX <= minX + 15 + distance
            && BoundingBox.MaxZ >= minZ - distance && BoundingBox.MinZ <= minZ + 15 + distance;
    }

    //AddAdditionalSaveData 写入额外数据到 CompoundTag 对应原版 addAdditionalSaveData
    //默认空实现子类按需覆盖持久化自定义字段
    protected virtual void AddAdditionalSaveData(CompoundTag tag) { }

    //WriteSaveData 序列化部件到 CompoundTag 对应原版 StructurePiece.createTag
    //字段名与取值方式逐字对齐原版 id 是类型注册名 BB 是六个整数的包围盒
    public CompoundTag WriteSaveData()
    {
        var tag = new CompoundTag();
        tag.PutString("id", (PieceType?.Id ?? Identifier.WithDefaultNamespace("invalid")).ToString());
        tag.PutIntArray("BB", new[]
        {
            BoundingBox.MinX, BoundingBox.MinY, BoundingBox.MinZ,
            BoundingBox.MaxX, BoundingBox.MaxY, BoundingBox.MaxZ,
        });
        //朝向没有实现的载体 恒写 -1 与原版"无朝向"的取值一致
        tag.PutInt("O", -1);
        tag.PutInt("GD", GenDepth);
        AddAdditionalSaveData(tag);
        return tag;
    }

    //ReadBoundingBox 从 BB int 数组还原边界框 对应原版 BoundingBox.CODEC 的 int 流形态
    protected static BoundingBoxInt ReadBoundingBox(CompoundTag tag)
    {
        var array = tag.GetIntArray("BB")?.Value;
        if (array is null || array.Length < 6) return new BoundingBoxInt(0, 0, 0, 0, 0, 0);
        return new BoundingBoxInt(array[0], array[1], array[2], array[3], array[4], array[5]);
    }
}

//BoundingBoxInt 整数边界框对应原版 net.minecraft.world.level.levelgen.structure.BoundingBox
//简化为 int 六元组持有 minX/maxX/minY/maxY/minZ/maxZ
public sealed record BoundingBoxInt(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    //FromChunkPos 从 ChunkPos 构造 16x16x384 边界框对应原版 chunk 区域
    public static BoundingBoxInt FromChunkPos(ChunkPos pos, int minY = -64, int maxY = 320)
        => new(pos.X << 4, minY, pos.Z << 4, (pos.X << 4) + 15, maxY, (pos.Z << 4) + 15);

    public int LengthX => MaxX - MinX + 1;
    public int LengthY => MaxY - MinY + 1;
    public int LengthZ => MaxZ - MinZ + 1;

    //IsInside 坐标是否落在盒内 对应原版 BoundingBox.isInside
    //地形适配核函数只对影响范围内的坐标求值
    public bool IsInside(int x, int y, int z)
        => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;

    //Intersects 检测两边界框是否相交对应原版 BoundingBox.intersects
    public bool Intersects(BoundingBoxInt other)
        => MaxX >= other.MinX && MinX <= other.MaxX
        && MaxY >= other.MinY && MinY <= other.MaxY
        && MaxZ >= other.MinZ && MinZ <= other.MaxZ;

    //Encapsulate 合并两个边界框对应原版 BoundingBox.encapsulate
    public BoundingBoxInt Encapsulate(BoundingBoxInt other)
        => new(
            Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Min(MinZ, other.MinZ),
            Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY), Math.Max(MaxZ, other.MaxZ));

    //InflatedBy 六面等量外扩 对应原版 BoundingBox.inflatedBy
    //地形适配按 terrain_adaptation 外扩 12 格时用它
    public BoundingBoxInt InflatedBy(int amount)
        => new(MinX - amount, MinY - amount, MinZ - amount, MaxX + amount, MaxY + amount, MaxZ + amount);
}
