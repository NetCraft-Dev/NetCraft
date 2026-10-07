using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePoolElement pool element, maps to vanilla net.minecraft.world.level.levelgen.structure.pools.StructurePoolElement
//An element represents "what to put into the jigsaw": a single template, a template list, a feature, or nothing at all
public abstract class StructurePoolElement
{
    //Codec pool element polymorphic codec, dispatches by the element_type field, maps to vanilla StructurePoolElement.CODEC
    public static readonly Codec<StructurePoolElement> Codec = new StructurePoolElementDispatchCodec();

    private StructureTemplatePool.Projection? _projection;

    protected StructurePoolElement(StructureTemplatePool.Projection projection) => _projection = projection;

    //Projection projection; throws when never set, maps to vanilla getProjection
    public StructureTemplatePool.Projection Projection
        => _projection ?? throw new InvalidOperationException("pool element has no projection set");

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

    //GetType the element's type, maps to vanilla getType; it shadows object.GetType and keeps the same name for easy cross-reference
    public abstract new StructurePoolElementType GetType();

    //HandleDataMarker handles data-mode structure blocks inside the structure, subclasses override as needed
    public virtual void HandleDataMarker(WorldGenRegion level, StructureBlockInfo dataMarker, BlockPos position,
        Rotation rotation, RandomSource random, BoundingBoxInt chunkBB)
    {
    }

    //GroundLevelDelta offset from the ground heightmap when the structure is placed, maps to vanilla getGroundLevelDelta
    public virtual int GroundLevelDelta => 1;
}

//StructurePoolElementDispatchCodec pool element polymorphic codec, looks up STRUCTURE_POOL_ELEMENT by element_type and dispatches
internal sealed class StructurePoolElementDispatchCodec : ScalarCodec<StructurePoolElement>
{
    public override DataResult<StructurePoolElement> Parse<U>(DynamicOps<U> ops, U input)
    {
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return DataResult<StructurePoolElement>.Error(() => "pool element must be an object");
        var map = mapResult.GetOrThrow();

        var typeTag = map.Get("element_type");
        if (!typeTag.IsPresent) return DataResult<StructurePoolElement>.Error(() => "pool element is missing the element_type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent) return DataResult<StructurePoolElement>.Error(() => "element_type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null) return DataResult<StructurePoolElement>.Error(() => $"invalid pool element type: {typeText.GetOrThrow()}");

        var type = BuiltInRegistries.STRUCTURE_POOL_ELEMENT.GetValue(typeId.Value) as StructurePoolElementType;
        if (type is null) return DataResult<StructurePoolElement>.Error(() => $"unregistered pool element type: {typeId}");
        return type.Decode(ops, map);
    }

    //EncodeStart dispatches encoding by the element's actual type; each type's encoding carries the element_type field
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, StructurePoolElement value)
        => value.GetType().Encode(ops, value);
}
