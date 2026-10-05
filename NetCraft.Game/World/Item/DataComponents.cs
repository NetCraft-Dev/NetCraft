using NetCraft.Codec;
using NetCraft.DataFixer.Util;
using NetCraft.Game.World.Items.Component;
using NetCraft.Game.World.Items.Component.Predicates;
using NetCraft.Network;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//DataComponents 预定义组件类型对应原版 net.minecraft.core.component.DataComponents
//简化只实现简单值类型组件用 object 装箱对齐 Java Integer/Boolean/Unit 装箱语义
//持久化 Codec 供命令层物品组件语法使用 非持久化组件按原版传 null 保持 transient
//复杂业务类型组件（FoodProperties/Tool/Weapon/Enchantments/ItemLore 等）待业务子系统就绪后补全
//Bootstrap 由 ClientMain/ServerMain 在 NetCraftKernel.Initialize 之后调用
public static class DataComponents
{
    //MAX_STACK_SIZE 物品最大堆叠数
    public static readonly DataComponentType<object> MAX_STACK_SIZE = Register(
        "max_stack_size", IntObjectNbtCodec.Instance, new VarIntObjectCodec());

    //MAX_DAMAGE 物品最大耐久
    public static readonly DataComponentType<object> MAX_DAMAGE = Register(
        "max_damage", IntObjectNbtCodec.Instance, new VarIntObjectCodec());

    //DAMAGE 物品当前损坏值
    public static readonly DataComponentType<object> DAMAGE = Register(
        "damage", IntObjectNbtCodec.Instance, new VarIntObjectCodec());

    //REPAIR_COST 修复费用
    public static readonly DataComponentType<object> REPAIR_COST = Register(
        "repair_cost", IntObjectNbtCodec.Instance, new VarIntObjectCodec());

    //UNBREAKABLE 不可破坏标记
    public static readonly DataComponentType<object> UNBREAKABLE = Register(
        "unbreakable", UnitObjectNbtCodec.Instance, new UnitObjectCodec());

    //CREATIVE_SLOT_LOCK 创造模式槽锁 原版无持久化 Codec 保持 transient
    public static readonly DataComponentType<object> CREATIVE_SLOT_LOCK = Register(
        "creative_slot_lock", null, new UnitObjectCodec());

    //INTANGIBLE_PROJECTILE 无形弹射物
    public static readonly DataComponentType<object> INTANGIBLE_PROJECTILE = Register(
        "intangible_projectile", UnitObjectNbtCodec.Instance, new UnitObjectCodec());

    //ENCHANTMENT_GLINT_OVERRIDE 附魔光泽覆盖
    public static readonly DataComponentType<object> ENCHANTMENT_GLINT_OVERRIDE = Register(
        "enchantment_glint_override", BooleanObjectNbtCodec.Instance, new BooleanObjectCodec());

    //BEES 蜂巢里装的蜜蜂
    public static readonly DataComponentType<object> BEES = Register(
        "bees", new ObjectCodec<Bees>(Bees.Codec), new ObjectStreamCodec<Bees>(Bees.StreamCodec));

    //BUNDLE_CONTENTS 收纳袋内容
    public static readonly DataComponentType<object> BUNDLE_CONTENTS = Register(
        "bundle_contents", new ObjectCodec<BundleContents>(BundleContents.PersistentCodec), new ObjectStreamCodec<BundleContents>(BundleContents.StreamCodec));

    //BLOCK_ENTITY_DATA 方块物品携带的方块实体数据 放下方块时刷进新方块实体
    public static readonly DataComponentType<object> BLOCK_ENTITY_DATA = Register(
        "block_entity_data",
        new ObjectCodec<TypedEntityData<Holder<BlockEntityType<object>>>>(
            TypedEntityData<Holder<BlockEntityType<object>>>.CodecOf(HolderSetCodecs.BlockEntityTypeRef)),
        new ObjectStreamCodec<TypedEntityData<Holder<BlockEntityType<object>>>>(
            TypedEntityData<Holder<BlockEntityType<object>>>.StreamCodecOf(
                ByteBufCodecs.Holder(Registries.BLOCK_ENTITY_TYPE))));

    //CUSTOM_DATA 自由格式自定义数据 物品 NBT 谓词靠它判内容
    public static readonly DataComponentType<object> CUSTOM_DATA = Register(
        "custom_data",
        new ObjectCodec<CustomData>(CustomData.PersistentCodec),
        new ObjectStreamCodec<CustomData>(CustomData.StreamCodec));

    //FIREWORK_EXPLOSION 烟花爆炸效果
    public static readonly DataComponentType<object> FIREWORK_EXPLOSION = Register(
        "firework_explosion",
        new ObjectCodec<FireworkExplosion>(FireworkExplosion.Codec),
        new ObjectStreamCodec<FireworkExplosion>(FireworkExplosion.StreamCodec));

    //FIREWORKS 烟花火箭数据
    public static readonly DataComponentType<object> FIREWORKS = Register(
        "fireworks",
        new ObjectCodec<Fireworks>(Fireworks.Codec),
        new ObjectStreamCodec<Fireworks>(Fireworks.StreamCodec));

    //POTION_CONTENTS 药水内容
    public static readonly DataComponentType<object> POTION_CONTENTS = Register(
        "potion_contents",
        new ObjectCodec<PotionContents>(PotionContents.Codec),
        new ObjectStreamCodec<PotionContents>(PotionContents.StreamCodec));

    //CONTAINER 物品容器内容
    public static readonly DataComponentType<object> CONTAINER = Register(
        "container",
        new ObjectCodec<ItemContainerContents>(ItemContainerContents.Codec),
        new ObjectStreamCodec<ItemContainerContents>(ItemContainerContents.StreamCodec));

    //ENCHANTMENTS 物品附魔表
    public static readonly DataComponentType<object> ENCHANTMENTS = Register(
        "enchantments",
        new ObjectCodec<ItemEnchantments>(ItemEnchantments.Codec),
        new ObjectStreamCodec<ItemEnchantments>(ItemEnchantments.StreamCodec));

    //STORED_ENCHANTMENTS 附魔书内附魔表
    public static readonly DataComponentType<object> STORED_ENCHANTMENTS = Register(
        "stored_enchantments",
        new ObjectCodec<ItemEnchantments>(ItemEnchantments.Codec),
        new ObjectStreamCodec<ItemEnchantments>(ItemEnchantments.StreamCodec));

    //TRIM 盔甲纹饰
    public static readonly DataComponentType<object> TRIM = Register(
        "trim",
        new ObjectCodec<ArmorTrim>(ArmorTrim.Codec),
        new ObjectStreamCodec<ArmorTrim>(ArmorTrim.StreamCodec));

    //JUKEBOX_PLAYABLE 唱片机可播放曲目
    public static readonly DataComponentType<object> JUKEBOX_PLAYABLE = Register(
        "jukebox_playable",
        new ObjectCodec<JukeboxPlayable>(JukeboxPlayable.Codec),
        new ObjectStreamCodec<JukeboxPlayable>(JukeboxPlayable.StreamCodec));

    //WRITABLE_BOOK_CONTENT 书与笔内容
    public static readonly DataComponentType<object> WRITABLE_BOOK_CONTENT = Register(
        "writable_book_content",
        new ObjectCodec<WritableBookContent>(WritableBookContent.Codec),
        new ObjectStreamCodec<WritableBookContent>(WritableBookContent.StreamCodec));

    //WRITTEN_BOOK_CONTENT 成书内容
    public static readonly DataComponentType<object> WRITTEN_BOOK_CONTENT = Register(
        "written_book_content",
        new ObjectCodec<WrittenBookContent>(WrittenBookContent.Codec),
        new ObjectStreamCodec<WrittenBookContent>(WrittenBookContent.StreamCodec));

    //VILLAGER_VARIANT 村民变体 值是指向村民类型注册表的引用
    public static readonly DataComponentType<object> VILLAGER_VARIANT = Register(
        "villager_variant",
        new ObjectCodec<Holder<VillagerType>>(HolderSetCodecs.VillagerTypeRef),
        new ObjectStreamCodec<Holder<VillagerType>>(ByteBufCodecs.Holder(Registries.VILLAGER_TYPE)));

    //Register 注册单个组件类型到 BuiltInRegistries.DATA_COMPONENT_TYPE
    private static DataComponentType<object> Register(
        string name, Codec<object>? codec, StreamCodec<RegistryFriendlyByteBuf, object> streamCodec)
    {
        var type = new SimpleDataComponentType<object>(codec, streamCodec, false);
        Registry<object>.Register(BuiltInRegistries.DATA_COMPONENT_TYPE, name, type);
        return type;
    }

    //Bootstrap 注册所有预定义组件类型由 ClientMain/ServerMain 调用
    //C# 静态字段在类首次访问时初始化此方法强制触发确保注册时机
    public static void Bootstrap()
    {
        _ = MAX_STACK_SIZE;
        _ = BEES;
        _ = BUNDLE_CONTENTS;
        _ = BLOCK_ENTITY_DATA;
        _ = FIREWORK_EXPLOSION;
        _ = FIREWORKS;
        _ = POTION_CONTENTS;
        _ = CONTAINER;
        _ = ENCHANTMENTS;
        _ = STORED_ENCHANTMENTS;
        _ = TRIM;
        _ = JUKEBOX_PLAYABLE;
        _ = WRITABLE_BOOK_CONTENT;
        _ = WRITTEN_BOOK_CONTENT;
        _ = VILLAGER_VARIANT;
        DataComponentPredicates.Bootstrap();
    }
}

//ObjectStreamCodec 把某个引用类型的流编解码适配成 object 版 供 DataComponents 注册复杂组件
internal sealed class ObjectStreamCodec<T> : StreamCodec<RegistryFriendlyByteBuf, object> where T : class
{
    private readonly StreamCodec<RegistryFriendlyByteBuf, T> _inner;

    public ObjectStreamCodec(StreamCodec<RegistryFriendlyByteBuf, T> inner) => _inner = inner;

    public object Decode(RegistryFriendlyByteBuf buf) => _inner.Decode(buf);

    public void Encode(RegistryFriendlyByteBuf buf, object value) => _inner.Encode(buf, (T)value);
}

//ObjectCodec 把某个引用类型的持久化编解码适配成 object 版 与 ObjectStreamCodec 对应
internal sealed class ObjectCodec<T> : ScalarCodec<object> where T : class
{
    private readonly Codec<T> _inner;

    public ObjectCodec(Codec<T> inner) => _inner = inner;

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input) => _inner.Parse(ops, input).Map(value => (object)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value) => _inner.EncodeStart(ops, (T)value);
}

//VarIntObjectCodec int 装箱为 object 的 StreamCodec 用 VarInt 编解码
internal sealed class VarIntObjectCodec : StreamCodec<RegistryFriendlyByteBuf, object>
{
    public object Decode(RegistryFriendlyByteBuf buf) => buf.ReadVarInt();

    public void Encode(RegistryFriendlyByteBuf buf, object value) => buf.WriteVarInt((int)value);
}

//BooleanObjectCodec bool 装箱为 object 的 StreamCodec 读 1 字节
internal sealed class BooleanObjectCodec : StreamCodec<RegistryFriendlyByteBuf, object>
{
    public object Decode(RegistryFriendlyByteBuf buf) => buf.ReadBoolean();

    public void Encode(RegistryFriendlyByteBuf buf, object value) => buf.WriteBoolean((bool)value);
}

//UnitObjectCodec Unit 装箱为 object 的 StreamCodec 无 payload 编解码返回 Unit.Instance
internal sealed class UnitObjectCodec : StreamCodec<RegistryFriendlyByteBuf, object>
{
    public object Decode(RegistryFriendlyByteBuf buf) => Unit.Instance;

    public void Encode(RegistryFriendlyByteBuf buf, object value) { }
}

//IntObjectNbtCodec int 组件持久化 Codec 与 NBT Int 互转
internal sealed class IntObjectNbtCodec : ScalarCodec<object>
{
    public static readonly IntObjectNbtCodec Instance = new();

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
        => Codecs.Int.Parse(ops, input).Map(value => (object)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value)
        => Codecs.Int.EncodeStart(ops, (int)value);
}

//BooleanObjectNbtCodec bool 组件持久化 Codec 与 NBT Byte 互转
internal sealed class BooleanObjectNbtCodec : ScalarCodec<object>
{
    public static readonly BooleanObjectNbtCodec Instance = new();

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
        => Codecs.Bool.Parse(ops, input).Map(value => (object)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value)
        => Codecs.Bool.EncodeStart(ops, (bool)value);
}

//UnitObjectNbtCodec unit 组件持久化 Codec 对齐原版 Unit.CODEC 解析恒成功编码成空
internal sealed class UnitObjectNbtCodec : ScalarCodec<object>
{
    public static readonly UnitObjectNbtCodec Instance = new();

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
        => DataResult<object>.Success(Unit.Instance);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value)
        => DataResult<U>.Success(ops.Empty());
}
