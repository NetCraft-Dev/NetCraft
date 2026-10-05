using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//DataComponentPredicates 数据组件谓词类型注册 对应原版 net.minecraft.core.component.predicates.DataComponentPredicates
//Bootstrap 由 DataComponents.Bootstrap 一并调用 必须在注册表冻结之前
public static class DataComponentPredicates
{
    //DAMAGE 耐久与损坏值谓词
    public static readonly ConcreteType<DamagePredicate> DAMAGE = Register("damage", DamagePredicate.Codec);

    //Bootstrap 触发静态字段初始化完成注册
    public static void Bootstrap()
    {
        _ = DAMAGE;
    }

    //Register 注册谓词类型到 DATA_COMPONENT_PREDICATE_TYPE 注册表
    private static ConcreteType<T> Register<T>(string name, Codec<T> codec)
        where T : class, DataComponentPredicate
    {
        var type = new ConcreteType<T>(codec);
        Registry<DataComponentPredicateType<object>>.Register(
            BuiltInRegistries.DATA_COMPONENT_PREDICATE_TYPE, name, type);
        return type;
    }
}
