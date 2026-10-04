using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePoolElement 池元素 对应原版 net.minecraft.world.level.levelgen.structure.pools.StructurePoolElement
//一个元素代表"往拼图里放什么" 可以是单模板 模板列表 特征 或者什么都不放
public abstract class StructurePoolElement
{
    //Codec 池元素多态 codec 按 element_type 字段派发 对应原版 StructurePoolElement.CODEC
    public static readonly Codec<StructurePoolElement> Codec = new StructurePoolElementDispatchCodec();

    private StructureTemplatePool.Projection? _projection;

    protected StructurePoolElement(StructureTemplatePool.Projection projection) => _projection = projection;

    //Projection 投影 没设过就抛异常 对应原版 getProjection
    public StructureTemplatePool.Projection Projection
        => _projection ?? throw new InvalidOperationException("池元素没有设置投影");

    public virtual StructurePoolElement SetProjection(StructureTemplatePool.Projection projection)
    {
        _projection = projection;
        return this;
    }

    public abstract Vec3i GetSize(StructureTemplateManager manager, Rotation rotation);

    public abstract List<StructureTemplate.JigsawBlockInfo> GetShuffledJigsawBlocks(StructureTemplateManager manager,
        BlockPos position, Rotation rotation, RandomSource random);

    public abstract BoundingBoxInt GetBoundingBox(StructureTemplateManager manager, BlockPos position, Rotation rotation);

    public abstract bool Place(StructureTemplateManager manager, WorldGenRegion level, StructureManager structureManager,
        ChunkGenerator generator, BlockPos position, BlockPos referencePos, Rotation rotation, BoundingBoxInt chunkBB,
        RandomSource random, LiquidSettings liquidSettings, bool keepJigsaws);

    //GetType 该元素的类型 对应原版 getType 会遮蔽 object.GetType 取名一致便于对照原版
    public abstract new StructurePoolElementType GetType();

    //HandleDataMarker 处理结构里的 data 模式结构方块 子类按需覆盖
    public virtual void HandleDataMarker(WorldGenRegion level, StructureBlockInfo dataMarker, BlockPos position,
        Rotation rotation, RandomSource random, BoundingBoxInt chunkBB)
    {
    }

    //GroundLevelDelta 结构落位时相对地面高度图的偏移 对应原版 getGroundLevelDelta
    public virtual int GroundLevelDelta => 1;
}

//StructurePoolElementDispatchCodec 池元素多态 codec 按 element_type 查 STRUCTURE_POOL_ELEMENT 再派发
internal sealed class StructurePoolElementDispatchCodec : ScalarCodec<StructurePoolElement>
{
    public override DataResult<StructurePoolElement> Parse<U>(DynamicOps<U> ops, U input)
    {
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return DataResult<StructurePoolElement>.Error(() => "池元素必须是对象");
        var map = mapResult.GetOrThrow();

        var typeTag = map.Get("element_type");
        if (!typeTag.IsPresent) return DataResult<StructurePoolElement>.Error(() => "池元素缺少 element_type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent) return DataResult<StructurePoolElement>.Error(() => "element_type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null) return DataResult<StructurePoolElement>.Error(() => $"非法的池元素类型: {typeText.GetOrThrow()}");

        var type = BuiltInRegistries.STRUCTURE_POOL_ELEMENT.GetValue(typeId.Value) as StructurePoolElementType;
        if (type is null) return DataResult<StructurePoolElement>.Error(() => $"未注册的池元素类型: {typeId}");
        return type.Decode(ops, map);
    }

    //EncodeStart 按元素实际类型派发编码 各类型编码后自带 element_type 字段
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, StructurePoolElement value)
        => value.GetType().Encode(ops, value);
}
