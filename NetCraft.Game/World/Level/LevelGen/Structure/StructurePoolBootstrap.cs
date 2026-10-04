namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePoolBootstrap 模板池子系统类型登记 对应原版 StructurePoolElementType 的静态登记
//各类型的静态 Instance 幂等注册 这里统一触碰一次保证在注册表 freeze 之前完成
public static class StructurePoolBootstrap
{
    public static void RegisterAll()
    {
        _ = SinglePoolElementType.Instance;
        _ = LegacySinglePoolElementType.Instance;
        _ = ListPoolElementType.Instance;
        _ = FeaturePoolElementType.Instance;
        _ = EmptyPoolElementType.Instance;
        //池别名类型必须先于拼图结构 json 装载就位 否则 pool_aliases 的 type 派发找不到目标
        PoolAliasBindings.RegisterAll();
        //拼图结构类型登记进 STRUCTURE_TYPE 供 worldgen/structure 按 type 派发
        _ = JigsawStructure.JigsawType;
    }
}
