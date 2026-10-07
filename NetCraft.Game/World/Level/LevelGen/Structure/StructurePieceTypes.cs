using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePieceSerializationContext piece serialization context, maps to the identically named vanilla class
//Restoring a piece from NBT needs runtime resources, and the structure template manager only exists on the world assembly side
public sealed class StructurePieceSerializationContext
{
    //TemplateManager structure template manager, null when not injected; pool element pieces then cannot restore their template
    public StructureTemplateManager? TemplateManager { get; }

    public StructurePieceSerializationContext(StructureTemplateManager? templateManager)
        => TemplateManager = templateManager;

    //FromManager wraps the template manager injected by world assembly into a context
    public static StructurePieceSerializationContext FromManager(StructureTemplateManager? manager)
        => new(manager);
}

//StructurePieceType structure piece type, maps to vanilla net.minecraft.world.level.levelgen.structure.pieces.StructurePieceType
//A type is one implementation of "restore a piece from NBT"; the id written to disk is what looks it back up
public abstract class StructurePieceType : NetCraft.Registry.StructurePieceType
{
    public Identifier Id { get; }

    protected StructurePieceType(Identifier id) => Id = id;

    //Load restores a piece from NBT; the common fields BB/O/GD are consumed by the piece base constructor
    public abstract NetCraft.Game.World.Level.LevelGen.StructurePiece Load(
        StructurePieceSerializationContext context, CompoundTag tag);

    //Register registers into STRUCTURE_PIECE and returns itself so a static field can be assigned directly
    protected static T Register<T>(Identifier id, T type) where T : StructurePieceType
    {
        Registry<NetCraft.Registry.StructurePieceType>.Register(BuiltInRegistries.STRUCTURE_PIECE, id, type);
        return type;
    }

    public override string ToString() => $"StructurePieceType[{Id}]";
}

//JigsawPieceType jigsaw piece type, registry name matches vanilla
//Almost all structure pieces in the world are pool element pieces; without it a jigsaw structure is an empty shell after load
public sealed class JigsawPieceType : StructurePieceType
{
    public static readonly JigsawPieceType Instance =
        Register(Identifier.WithDefaultNamespace("jigsaw"), new JigsawPieceType());

    private JigsawPieceType()
        : base(Identifier.WithDefaultNamespace("jigsaw")) { }

    public override NetCraft.Game.World.Level.LevelGen.StructurePiece Load(
        StructurePieceSerializationContext context, CompoundTag tag)
        => new PoolElementStructurePiece(context, tag);
}

//PillarPieceType pillar piece type used by this project's sample structure; no vanilla counterpart
public sealed class PillarPieceType : StructurePieceType
{
    public static readonly PillarPieceType Instance =
        Register(Identifier.WithDefaultNamespace("pillar"), new PillarPieceType());

    private PillarPieceType()
        : base(Identifier.WithDefaultNamespace("pillar")) { }

    public override NetCraft.Game.World.Level.LevelGen.StructurePiece Load(
        StructurePieceSerializationContext context, CompoundTag tag)
        => NetCraft.Game.World.Level.LevelGen.Structures.PillarStructurePiece.FromTag(tag);
}

//StructurePieceBootstrap piece type registration entry point
public static class StructurePieceBootstrap
{
    //RegisterAll registers all piece types and must complete before structures load, or a piece id cannot find its type
    public static void RegisterAll()
    {
        _ = JigsawPieceType.Instance;
        _ = PillarPieceType.Instance;
    }
}
