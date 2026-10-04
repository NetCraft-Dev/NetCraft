using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Game.World.Level.LevelGen.Structure;
//Structures 命名空间下的 LevelGen 有同名子命名空间 Structure 必须别名区分
using StructureBase = NetCraft.Game.World.Level.LevelGen.Structure.Structure;

namespace NetCraft.Game.World.Level.LevelGen.Structures;

//PillarStructureFeature 单根石柱示例结构 对应原版最简 Structure 子类
//用于验证 STRUCTURE_START 装配链路 不放进任何结构集合里所以不会自然生成
public sealed class PillarStructureFeature : StructureBase
{
    //PillarType 石柱类型 注册名与结构同名
    public static readonly StructureType PillarType = new(Identifier.WithDefaultNamespace("pillar"));

    public const int PillarHeight = 16;
    public const int BaseY = 64;

    public override Identifier Id => Identifier.WithDefaultNamespace("pillar");

    public override StructureType Type => PillarType;

    //群系声明为空即不放行群系过滤 示例结构不参与自然生成
    public PillarStructureFeature() : base(StructureGenerationSettings.Default) { }

    //FindGenerationPoint 在 chunk 中央生成一根石柱
    public override GenerationStub? FindGenerationPoint(GenerationContext context)
    {
        var chunkPos = context.ChunkPos;
        var baseX = chunkPos.X * 16 + 8;
        var baseZ = chunkPos.Z * 16 + 8;
        var piece = new PillarStructurePiece(baseX, BaseY, baseZ, PillarHeight);
        return new GenerationStub(new BlockPos(baseX, BaseY, baseZ),
            new StructurePiece[] { piece });
    }
}
