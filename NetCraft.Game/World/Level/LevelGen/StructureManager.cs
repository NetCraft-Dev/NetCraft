using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.LevelGen;

//StructureManager 结构管理器适配器对应原版 net.minecraft.world.level.StructureManager
//阶段 E 升级为 StructureFeatureManager 的薄包装对外暴露 StructureManager API
//内部委托 StructureFeatureManager 实现真实结构查询逻辑
public sealed class StructureManager
{
    private readonly StructureFeatureManager _featureManager;

    public StructureManager(StructureFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    //Default 默认空 StructureManager 用于无结构场景
    public static StructureManager Default => new(new StructureFeatureManager());

    public bool HasStructureReferences(ChunkPos pos)
        => _featureManager.HasStructureReferences(pos);

    public bool HasStructureStartsForChunk(ChunkAccess chunk)
        => _featureManager.HasStructureStartsForChunk(chunk);

    public IReadOnlyCollection<StructureStart> GetStructureStarts(ChunkPos pos)
        => _featureManager.GetStructureStarts(pos);

    public void AddStructureStart(ChunkPos pos, StructureStart start)
        => _featureManager.AddStructureStart(pos, start);

    public void AddStructureReference(ChunkPos pos, StructureReference reference)
        => _featureManager.AddStructureReference(pos, reference);
}
