using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//DataComponentPredicates 数据组件谓词类型注册 对应原版 net.minecraft.core.component.predicates.DataComponentPredicates
//Bootstrap 由 DataComponents.Bootstrap 一并调用 必须在注册表冻结之前
public static class DataComponentPredicates
{
    //BUNDLE_CONTENTS 收纳袋内容谓词
    public static readonly ConcreteType<BundlePredicate> BUNDLE_CONTENTS =
        Register("bundle_contents", BundlePredicate.Codec);

    //CONTAINER 容器谓词
    public static readonly ConcreteType<ContainerPredicate> CONTAINER =
        Register("container", ContainerPredicate.Codec);

    //CUSTOM_DATA 自定义数据谓词
    public static readonly ConcreteType<CustomDataPredicate> CUSTOM_DATA =
        Register("custom_data", CustomDataPredicate.Codec);

    //DAMAGE 耐久与损坏值谓词
    public static readonly ConcreteType<DamagePredicate> DAMAGE = Register("damage", DamagePredicate.Codec);

    //FIREWORK_EXPLOSION 烟花爆炸谓词
    public static readonly ConcreteType<FireworkExplosionPredicate> FIREWORK_EXPLOSION =
        Register("firework_explosion", FireworkExplosionPredicate.Codec);

    //FIREWORKS 烟花火箭谓词
    public static readonly ConcreteType<FireworksPredicate> FIREWORKS =
        Register("fireworks", FireworksPredicate.Codec);

    //POTION_CONTENTS 药水谓词
    public static readonly ConcreteType<PotionsPredicate> POTION_CONTENTS =
        Register("potion_contents", PotionsPredicate.Codec);

    //VILLAGER_VARIANT 村民变体谓词
    public static readonly ConcreteType<VillagerTypePredicate> VILLAGER_VARIANT =
        Register("villager_variant", VillagerTypePredicate.Codec);

    //WRITABLE_BOOK_CONTENT 书与笔谓词
    public static readonly ConcreteType<WritableBookPredicate> WRITABLE_BOOK_CONTENT =
        Register("writable_book_content", WritableBookPredicate.Codec);

    //WRITTEN_BOOK_CONTENT 成书谓词
    public static readonly ConcreteType<WrittenBookPredicate> WRITTEN_BOOK_CONTENT =
        Register("written_book_content", WrittenBookPredicate.Codec);

    //Bootstrap 触发静态字段初始化完成注册
    public static void Bootstrap()
    {
        _ = BUNDLE_CONTENTS;
        _ = CONTAINER;
        _ = CUSTOM_DATA;
        _ = DAMAGE;
        _ = FIREWORK_EXPLOSION;
        _ = FIREWORKS;
        _ = POTION_CONTENTS;
        _ = VILLAGER_VARIANT;
        _ = WRITABLE_BOOK_CONTENT;
        _ = WRITTEN_BOOK_CONTENT;
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
