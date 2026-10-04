using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePieceSerializationContext 片段序列化上下文 对应原版同名类
//片段从 NBT 还原时要拿运行期资源 结构模板管理器只有世界装配侧才有
public sealed class StructurePieceSerializationContext
{
    //TemplateManager 结构模板管理器 未注入时为 null 此时池元素片段还原不出模板
    public StructureTemplateManager? TemplateManager { get; }

    public StructurePieceSerializationContext(StructureTemplateManager? templateManager)
        => TemplateManager = templateManager;

    //FromManager 把世界装配注入的模板管理器包成上下文
    public static StructurePieceSerializationContext FromManager(StructureTemplateManager? manager)
        => new(manager);
}

//StructurePieceType 结构片段类型 对应原版 net.minecraft.world.level.levelgen.structure.pieces.StructurePieceType
//一个类型就是"按 NBT 还原片段"的一份实现 片段落盘写的 id 就是拿它查回来的
public abstract class StructurePieceType : NetCraft.Registry.StructurePieceType
{
    public Identifier Id { get; }

    protected StructurePieceType(Identifier id) => Id = id;

    //Load 从 NBT 还原片段 通用字段 BB/O/GD 由片段基类构造消费
    public abstract NetCraft.Game.World.Level.LevelGen.StructurePiece Load(
        StructurePieceSerializationContext context, CompoundTag tag);

    //Register 注册进 STRUCTURE_PIECE 并返回自身 便于静态字段直接赋值
    protected static T Register<T>(Identifier id, T type) where T : StructurePieceType
    {
        Registry<NetCraft.Registry.StructurePieceType>.Register(BuiltInRegistries.STRUCTURE_PIECE, id, type);
        return type;
    }

    public override string ToString() => $"StructurePieceType[{Id}]";
}

//JigsawPieceType 拼图片段类型 注册名与原版一致
//世界里绝大多数结构片段都是池元素片段 缺它 jigsaw 结构读档后只剩空壳
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

//PillarPieceType 石柱片段类型 本项目示例结构用 原版没有对应物
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

//StructurePieceBootstrap 片段类型登记入口
public static class StructurePieceBootstrap
{
    //RegisterAll 登记全部片段类型 必须在结构读档之前完成 否则片段的 id 查不到类型
    public static void RegisterAll()
    {
        _ = JigsawPieceType.Instance;
        _ = PillarPieceType.Instance;
    }
}
