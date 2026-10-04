using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureStart 结构装配结果 对应原版 StructureStart
//持结构引用与片段列表 有效性只看片段是否为空
//包围盒是全体片段的包络 地形适配还要按 terrain_adaptation 再外扩
public sealed class StructureStart
{
    //InvalidStartId 无效标记 对应原版 INVALID_START_ID
    public const string InvalidStartId = "invalid";

    //Invalid 无效结果 生成失败时返回它而不是 null 调用方按 IsValid 判断
    public static readonly StructureStart Invalid = new();

    //Structure 结构引用 无效时为 null
    public NetCraft.Registry.Structure? Structure { get; }

    //StructureId 结构注册名 无效时是 invalid
    public Identifier StructureId => Structure is Structure structure
        ? structure.Id
        : Identifier.WithDefaultNamespace(InvalidStartId);

    public ChunkPos ChunkPos { get; }

    //Pieces 片段列表 空列表即无效
    public IReadOnlyList<StructurePiece> Pieces { get; }

    //BoundingBox 全体片段的包络 无片段时是零盒
    public BoundingBoxInt BoundingBox { get; }

    //IsValid 有效判定 对应原版 isValid 只看片段是否为空
    public bool IsValid => Pieces.Count > 0;

    //References 已被相邻区块引用过几次 对应原版 references
    public int References { get; private set; }

    //MaxReferences 引用次数上限 超过之后相邻区块不再复用本装配结果
    public const int MaxReferences = 1;

    //CanBeReferenced 还能被引用 对应原版 canBeReferenced
    public bool CanBeReferenced => References < MaxReferences;

    //AddReference 引用计数加一 对应原版 addReference
    public void AddReference() => References++;

    //CreateTag 序列化装配结果 对应原版 StructureStart.createTag
    //字段名与取值方式逐字对齐原版 片段自身字段由各自 WriteSaveData 写
    public CompoundTag CreateTag()
    {
        var tag = new CompoundTag();
        if (!IsValid)
        {
            //无效装配只落一个标记 读回来就是无效结果 不会污染区块的结构表
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

    //LoadStaticStart 从 NBT 还原装配结果 对应原版 loadStaticStart
    //结构注册名查不到返回 null 由调用方跳过 单个片段类型不认识则跳过该片段
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
                //片段类型不认识的按原版跳过 保留其余片段比整条装配失败好
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

    //AdjustBoundingBox 按地形适配外扩包围盒 对应原版 Structure.adjustBoundingBox
    //需要改编地形时外扩 12 格 噪声阶段的地形核函数用它算影响范围
    public BoundingBoxInt AdjustBoundingBox()
        => Structure is Structure structure
            ? BoundingBox.InflatedBy(structure.Settings.TerrainAdaptation.BeardEdgeNeeded())
            : BoundingBox;

    //PlaceInChunk 把落在目标区块内的片段写进世界 对应原版 StructureStart.placeInChunk
    //只处理与目标区块相交的片段 越界写入由 WorldGenRegion 按写入半径静默丢弃
    //装饰阶段每步先结构后特征 结构先落地才能让特征在结构上生长
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
