namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureProcessorBootstrap 处理器子系统类型登记 对应原版 StructureProcessorTypes 与三张规则相关注册表
//各类型的静态字段幂等注册 这里统一触碰一次保证在注册表 freeze 之前完成
public static class StructureProcessorBootstrap
{
    public static void RegisterAll()
    {
        //处理器类型先登记 它的 codec 会引用规则测试与方块实体修改器的解码路径
        StructureProcessorTypes.RegisterAll();
        //规则测试 位置判定 方块实体修改器三张表触碰任一个静态字段即完成全部登记
        _ = RuleTestTypes.AlwaysTrue;
        _ = PosRuleTestTypes.AlwaysTrue;
        _ = RuleBlockEntityModifierTypes.Passthrough;
    }
}
