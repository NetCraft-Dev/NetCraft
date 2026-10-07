using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Util.Random;
using MiscCodecs = NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc.RotationCodec;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//PoolElementStructurePiece pool element piece, maps to vanilla net.minecraft.world.level.levelgen.structure.PoolElementStructurePiece
//Placement of a pool element in the assembly result: position, rotation, ground level delta and all junctions it connects to
public sealed class PoolElementStructurePiece : StructurePiece
{
    private readonly List<JigsawJunction> _junctions = new();
    //On the load path the template manager comes from the context; when not injected, a restored piece cannot place anything
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

    //Restores a pool element piece from NBT, maps to the vanilla PoolElementStructurePiece(context, tag) constructor
    //The template manager is not saved; it comes from the context, injected by world assembly on load
    public PoolElementStructurePiece(StructurePieceSerializationContext context, CompoundTag tag)
        : base(JigsawPieceType.Instance, tag)
    {
        _structureTemplateManager = context.TemplateManager;
        _position = new BlockPos(tag.GetIntOr("PosX", 0), tag.GetIntOr("PosY", 0), tag.GetIntOr("PosZ", 0));
        GroundLevelDelta = tag.GetIntOr("ground_level_delta", 0);
        var elementResult = tag.Read("pool_element", StructurePoolElement.Codec);
        if (!elementResult.IsPresent) throw new InvalidOperationException("pool element piece is missing the pool_element field");
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

    //Element the pool element this piece places
    public StructurePoolElement Element { get; }

    //Position the piece placement position, maps to vanilla getPosition
    public BlockPos Position => _position;

    //GroundLevelDelta offset from the ground heightmap, maps to vanilla getGroundLevelDelta
    public int GroundLevelDelta { get; }

    public Rotation Rotation { get; }

    //Junctions the junctions this piece connects to; terrain adaptation smooths by them
    public IReadOnlyList<JigsawJunction> Junctions => _junctions;

    public void AddJunction(JigsawJunction junction) => _junctions.Add(junction);

    //Move translates the piece, moving position and bounding box together, maps to vanilla move
    public override void Move(int dx, int dy, int dz)
    {
        base.Move(dx, dy, dz);
        _position = _position.Offset(dx, dy, dz);
    }

    //AddAdditionalSaveData writes the pool element piece's own fields, maps to the identically named vanilla method
    //The template manager is not saved; it is re-injected by world assembly on load
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
        //The default liquid settings are not written; vanilla falls back to the default on read, and omitting one field removes one compatibility burden
        if (_liquidSettings != JigsawStructure.DefaultLiquidSettings)
            tag.Store("liquid_settings", StructurePoolCodecs.LiquidSettingsCodec, NbtOps.Instance, _liquidSettings);
    }

    //PostProcess writes the pool element into the world, maps to vanilla postProcess
    //The actual write goes through element.Place, called chunk by chunk during decoration
    public void PostProcess(WorldGenRegion level, StructureManager structureManager, ChunkGenerator generator,
        RandomSource random, BoundingBoxInt chunkBB, BlockPos referencePos)
        => Place(level, structureManager, generator, random, chunkBB, referencePos, false);

    //Place places the pool element; when keepJigsaws is true the jigsaw blocks are kept instead of becoming their final state
    public void Place(WorldGenRegion level, StructureManager structureManager, ChunkGenerator generator,
        RandomSource random, BoundingBoxInt chunkBB, BlockPos referencePos, bool keepJigsaws)
        //World assembly always injects the template manager, so it is non-null by the time placement runs
        => Element.Place(_structureTemplateManager!, level, structureManager, generator, _position, referencePos,
            Rotation, chunkBB, random, _liquidSettings, keepJigsaws);

    public override string ToString()
        => $"PoolElementStructurePiece[{_position} {Rotation} {Element}]";
}
