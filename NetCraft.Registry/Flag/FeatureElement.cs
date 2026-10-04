namespace NetCraft.Registry.Flag;

//带特性开关的元素对应原版FeatureElement
//方块物品实体类型这些实现它，来说明自己需要哪些开关
public interface FeatureElement
{
    //会被特性开关过滤掉的注册表
    //原版是带通配符的Set，C#没有协变通配只能存object，比较时按注册表名对
    static readonly object[] FILTERED_REGISTRIES =
    [
        Registries.ITEM,
        Registries.BLOCK,
        Registries.ENTITY_TYPE,
        Registries.GAME_RULE,
        Registries.MENU,
        Registries.POTION,
        Registries.MOB_EFFECT,
    ];

    FeatureFlagSet RequiredFeatures();

    //需要的开关都被打开才算启用
    bool IsEnabled(FeatureFlagSet enabledFeatures) => RequiredFeatures().IsSubsetOf(enabledFeatures);
}
