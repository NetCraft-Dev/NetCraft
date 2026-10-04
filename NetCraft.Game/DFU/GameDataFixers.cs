using NetCraft.DataFixer;
using NetCraft.Game.DFU.Fixes;
using NetCraft.Game.DFU.Schemas;

namespace NetCraft.Game.DFU;

//GameDataFixers业务层DFU注册器
//DFU内核只提供框架不主动注册Schema或Fix
//本类按V1_21版本段顺序注册13个Schema与18个具体Fix类构建可用的DataFixer
//覆盖1.20.2到1.21.4存档升级链
public static class GameDataFixers
{
    //V1_21段最大版本号对应1.21.4
    public const int V1_21_VERSION = 4312;

    //BuildV1_21Fixer按版本顺序注册全部Schema与Fix返回DataFixer实例
    //Schema按版本递增注册parent链自动链接到上一个Schema
    //Fix按版本递增注册每个Fix用对应的输出Schema
    //返回类型用全限定名避免与NetCraft.DataFixer命名空间同名冲突
    public static NetCraft.DataFixer.DataFixer BuildV1_21Fixer()
    {
        var builder = new DataFixerBuilder(V1_21_VERSION);

        //Schema注册顺序v99基础到v4312末尾
        //V1Foundation注册ENTITY/BLOCK_ENTITY等递归类型
        var v99 = builder.AddSchema(99, 0, (key, parent) => new V1Foundation(key, parent));
        var v2505 = builder.AddSchema(2505, 0, (key, parent) => new V2505(key, parent));
        var v3448 = builder.AddSchema(3448, 0, (key, parent) => new V3448(key, parent));
        var v3685 = builder.AddSchema(3685, 0, (key, parent) => new V3685(key, parent));
        //V3818_3原版是3818带subVersion 3
        var v3818_3 = builder.AddSchema(3818, 3, (key, parent) => new V3818_3(key, parent));
        var v3825 = builder.AddSchema(3825, 0, (key, parent) => new V3825(key, parent));
        var v3938 = builder.AddSchema(3938, 0, (key, parent) => new V3938(key, parent));
        var v4059 = builder.AddSchema(4059, 0, (key, parent) => new V4059(key, parent));
        var v4067 = builder.AddSchema(4067, 0, (key, parent) => new V4067(key, parent));
        var v4300 = builder.AddSchema(4300, 0, (key, parent) => new V4300(key, parent));
        var v4306 = builder.AddSchema(4306, 0, (key, parent) => new V4306(key, parent));
        var v4307 = builder.AddSchema(4307, 0, (key, parent) => new V4307(key, parent));
        var v4312 = builder.AddSchema(4312, 0, (key, parent) => new V4312(key, parent));

        //Fix注册按版本递增每个Fix用对应输出Schema
        //renames参数传identity函数让流程跑通实际rename映射由Game业务层后续补全
        builder.AddFixer(new MemoryExpiryDataFix(v2505, "minecraft:villager"));
        builder.AddFixer(new AttributesRenameLegacy(v3818_3, "Attributes rename (legacy)", id => id));
        builder.AddFixer(new DecoratedPotFieldRenameFix(v3448));
        builder.AddFixer(new FixProjectileStoredItem(v3685));
        builder.AddFixer(new RenameEnchantmentsFix(v3818_3, "Rename enchantments", new Dictionary<string, string>()));
        builder.AddFixer(new LodestoneCompassComponentFix(v3825));
        builder.AddFixer(new TrialSpawnerConfigFix(v3825));
        builder.AddFixer(new ProjectileStoredWeaponFix(v3938));
        builder.AddFixer(new AttributeModifierIdFix(v3938));
        builder.AddFixer(new JukeboxTicksSinceSongStartedFix(v3938));
        builder.AddFixer(new AttributeIdPrefixFix(v4059));
        builder.AddFixer(new FoodToConsumableFix(v4059));
        builder.AddFixer(new TrialSpawnerConfigInRegistryFix(v4067));
        builder.AddFixer(new EquippableAssetRenameFix(v4300));
        builder.AddFixer(new CustomModelDataExpandFix(v4300));
        builder.AddFixer(new DropChancesFormatFix(v4300));
        builder.AddFixer(new SaddleEquipmentSlotFix(v4300));
        builder.AddFixer(new TooltipDisplayComponentFix(v4307));

        return builder.Build().Fixer();
    }
}
