using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Util.Random;
using MiscCodecs = NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc.RotationCodec;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//PoolElementStructurePiece 池元素片段 对应原版 net.minecraft.world.level.levelgen.structure.PoolElementStructurePiece
//一个池元素在装配结果里的落位：位置 旋转 地面高度差 与它接出的全部接缝
public sealed class PoolElementStructurePiece : StructurePiece
{
    private readonly List<JigsawJunction> _junctions = new();
    //读档路径上模板管理器由上下文提供 未注入时片段还原出来也放不下东西
    private readonly StructureTemplateManager? _structureTemplateManager;
    private readonly LiquidSettings _liquidSettings;
    private BlockPos _position;

    public PoolElementStructurePiece(StructureTemplateManager structureTemplateManager, StructurePoolElement element,
        BlockPos position, int groundLevelDelta, Rotation rotation, BoundingBoxInt boundingBox,
        LiquidSettings liquidSettings)
        : base(JigsawPieceType.Instance, 0, boundingBox)
    {
        _structureTemplateManager = structureTemplateManager;
        Element = element;
        _position = position;
        GroundLevelDelta = groundLevelDelta;
        Rotation = rotation;
        _liquidSettings = liquidSettings;
    }

    //从 NBT 还原池元素片段 对应原版 PoolElementStructurePiece(context, tag) 构造
    //模板管理器不落盘 从上下文拿 读档时由世界装配注入
    public PoolElementStructurePiece(StructurePieceSerializationContext context, CompoundTag tag)
        : base(JigsawPieceType.Instance, tag)
    {
        _structureTemplateManager = context.TemplateManager;
        _position = new BlockPos(tag.GetIntOr("PosX", 0), tag.GetIntOr("PosY", 0), tag.GetIntOr("PosZ", 0));
        GroundLevelDelta = tag.GetIntOr("ground_level_delta", 0);
        var elementResult = tag.Read("pool_element", StructurePoolElement.Codec);
        if (!elementResult.IsPresent) throw new InvalidOperationException("池元素片段缺少 pool_element 字段");
        Element = elementResult.Get();
        Rotation = tag.Read("rotation", MiscCodecs.Instance).OrElse(Rotation.None);
        _liquidSettings = tag.Read("liquid_settings", StructurePoolCodecs.LiquidSettingsCodec)
            .OrElse(JigsawStructure.DefaultLiquidSettings);
        var junctionsTag = tag.GetList("junctions");
        if (junctionsTag is not null)
        {
            foreach (var entry in junctionsTag)
            {
                if (entry is not CompoundTag junctionTag) continue;
                var junctionResult = JigsawJunction.Codec.Parse(NbtOps.Instance, junctionTag).Result();
                if (junctionResult.IsPresent) _junctions.Add(junctionResult.Get());
            }
        }
    }

    //Element 该片段放置的池元素
    public StructurePoolElement Element { get; }

    //Position 片段落位坐标 对应原版 getPosition
    public BlockPos Position => _position;

    //GroundLevelDelta 相对地面高度图的偏移 对应原版 getGroundLevelDelta
    public int GroundLevelDelta { get; }

    public Rotation Rotation { get; }

    //Junctions 该片段接出的接缝 地形适配按它做平滑
    public IReadOnlyList<JigsawJunction> Junctions => _junctions;

    public void AddJunction(JigsawJunction junction) => _junctions.Add(junction);

    //Move 平移片段 位置与包围盒一起动 对应原版 move
    public override void Move(int dx, int dy, int dz)
    {
        base.Move(dx, dy, dz);
        _position = _position.Offset(dx, dy, dz);
    }

    //AddAdditionalSaveData 写入池元素片段自身字段 对应原版同名方法
    //模板管理器不落盘 读档时由世界装配重新注入
    protected override void AddAdditionalSaveData(CompoundTag tag)
    {
        tag.PutInt("PosX", _position.X);
        tag.PutInt("PosY", _position.Y);
        tag.PutInt("PosZ", _position.Z);
        tag.PutInt("ground_level_delta", GroundLevelDelta);
        tag.Store("pool_element", StructurePoolElement.Codec, NbtOps.Instance, Element);
        tag.Store("rotation", MiscCodecs.Instance, NbtOps.Instance, Rotation);
        var junctionsTag = new ListTag();
        foreach (var junction in _junctions)
            junctionsTag.Add(JigsawJunction.Codec.EncodeStart(NbtOps.Instance, junction).GetOrThrow());
        tag.Put("junctions", junctionsTag);
        //默认液体设置不写盘 原版读回时按默认值兜底 少写一个字段少一处兼容负担
        if (_liquidSettings != JigsawStructure.DefaultLiquidSettings)
            tag.Store("liquid_settings", StructurePoolCodecs.LiquidSettingsCodec, NbtOps.Instance, _liquidSettings);
    }

    //PostProcess 把池元素写进世界 对应原版 postProcess
    //真实写入走 element.Place 装饰阶段逐区块调用
    public void PostProcess(WorldGenRegion level, StructureManager structureManager, ChunkGenerator generator,
        RandomSource random, BoundingBoxInt chunkBB, BlockPos referencePos)
        => Place(level, structureManager, generator, random, chunkBB, referencePos, false);

    //Place 放置池元素 keepJigsaws 为真时保留拼图方块不替换成最终状态
    public void Place(WorldGenRegion level, StructureManager structureManager, ChunkGenerator generator,
        RandomSource random, BoundingBoxInt chunkBB, BlockPos referencePos, bool keepJigsaws)
        //世界装配一定注入过模板管理器 走到放置这一步必然非空
        => Element.Place(_structureTemplateManager!, level, structureManager, generator, _position, referencePos,
            Rotation, chunkBB, random, _liquidSettings, keepJigsaws);

    public override string ToString()
        => $"PoolElementStructurePiece[{_position} {Rotation} {Element}]";
}
