using NetCraft.Codec;

namespace NetCraft.Registry;

//HolderSetCodecs 注册表元素集合 codec 入口对应原版 RegistryCodecs.homogeneousList
//单个 id 字符串或 id 字符串数组都接受 前缀 # 视为标签引用
public static class HolderSetCodecs
{
    //BlockSet 方块集合 codec 供 carver 的 replaceable 字段使用 绑定 BuiltInRegistries.BLOCK
    public static readonly Codec<HolderSet<Block>> BlockSet = new HolderSetCodec<Block>(BuiltInRegistries.BLOCK);

    //ConfiguredCarverSet 配置化雕刻器集合 codec 供 biome 的 carvers 字段使用
    public static readonly Codec<HolderSet<ConfiguredWorldCarver>> ConfiguredCarverSet =
        new HolderSetCodec<ConfiguredWorldCarver>(BuiltInRegistries.CONFIGURED_CARVER);

    //PlacedFeatureSet 已放置特征集合 codec 供 biome 的 features 字段使用
    public static readonly Codec<HolderSet<PlacedFeature>> PlacedFeatureSet =
        new HolderSetCodec<PlacedFeature>(BuiltInRegistries.PLACED_FEATURE);

    //ConfiguredFeatureRef 单个配置化特征引用 codec 供 placed_feature 的 feature 字段使用
    public static readonly Codec<Holder<ConfiguredFeature>> ConfiguredFeatureRef =
        new HolderRefCodec<ConfiguredFeature>(BuiltInRegistries.CONFIGURED_FEATURE);

    //BiomeSet 群系集合 codec 供结构的 biomes 字段使用 支持 #标签 与 id 列表
    public static readonly Codec<HolderSet<Biome>> BiomeSet = new HolderSetCodec<Biome>(BuiltInRegistries.BIOME);

    //StructureRef 单个结构引用 codec 供结构集合的 structures 字段使用
    public static readonly Codec<Holder<Structure>> StructureRef =
        new HolderRefCodec<Structure>(BuiltInRegistries.STRUCTURE);

    //StructureSetRef 单个结构集合引用 codec 供放置排斥区的 other_set 字段使用
    public static readonly Codec<Holder<StructureSet>> StructureSetRef =
        new HolderRefCodec<StructureSet>(BuiltInRegistries.STRUCTURE_SET);

    //EntityTypeRef 单个实体类型引用 codec 供收纳袋的蜜蜂存档这类带类型标识的数据使用
    public static readonly Codec<Holder<EntityType<object>>> EntityTypeRef =
        new HolderRefCodec<EntityType<object>>(BuiltInRegistries.ENTITY_TYPE);

    //BlockEntityTypeRef 单个方块实体类型引用 codec 供方块物品携带的方块实体数据使用
    public static readonly Codec<Holder<BlockEntityType<object>>> BlockEntityTypeRef =
        new HolderRefCodec<BlockEntityType<object>>(BuiltInRegistries.BLOCK_ENTITY_TYPE);
}

//HolderRefCodec 单元素注册表引用 codec 对应原版 RegistryFileCodec 的字符串形式
//元素必须已在注册表里 未注册直接报错交给加载器重试
//不能退回未绑定 Reference: Register 只在 _byKey 里找既有 holder 不会复用游离实例 引用会永久断开
internal sealed class HolderRefCodec<T> : ScalarCodec<Holder<T>> where T : class
{
    private readonly Registry<T> _registry;

    public HolderRefCodec(Registry<T> registry) => _registry = registry;

    public override DataResult<Holder<T>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent)
            return DataResult<Holder<T>>.Error(() => "注册表元素引用必须是字符串");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null)
            return DataResult<Holder<T>>.Error(() => $"非法的标识符: {text.GetOrThrow()}");
        var holder = _registry.Get(id.Value);
        return holder is null
            ? DataResult<Holder<T>>.Error(() => $"注册表 {_registry.Key.Identifier} 里还没有 {id}")
            : DataResult<Holder<T>>.Success(holder);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<T> value)
    {
        var key = value.UnwrapKey();
        return key is null
            ? DataResult<U>.Error(() => "直接持有者没有注册名 无法编码")
            : DataResult<U>.Success(ops.CreateString(key.Identifier.ToString()));
    }
}

//HolderSetCodec 同名元素集合编解码 元素绑定到指定注册表
//注册表还没装载该元素时退化成未绑定 Reference 只保留注册名
internal sealed class HolderSetCodec<T> : ScalarCodec<HolderSet<T>> where T : class
{
    private readonly Registry<T> _registry;

    public HolderSetCodec(Registry<T> registry) => _registry = registry;

    public override DataResult<HolderSet<T>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent)
            return ParseOne(text.GetOrThrow());
        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent)
            return DataResult<HolderSet<T>>.Error(() => "HolderSet requires a string or a list of strings");
        var holders = new List<Holder<T>>();
        foreach (var element in stream.GetOrThrow())
        {
            var elementText = ops.GetStringValue(element);
            if (!elementText.Result().IsPresent)
                return DataResult<HolderSet<T>>.Error(() => "HolderSet element must be a string");
            holders.Add(Resolve(elementText.GetOrThrow()));
        }
        return DataResult<HolderSet<T>>.Success(new DirectHolderSet<T>(holders));
    }

    //ParseOne 单字符串形态 # 前缀解成标签集合 否则解成单元素直接集合
    private DataResult<HolderSet<T>> ParseOne(string text)
    {
        if (text.StartsWith('#'))
        {
            var tagId = Identifier.TryParse(text[1..]);
            if (tagId is null)
                return DataResult<HolderSet<T>>.Error(() => $"Invalid tag id: {text}");
            var tag = TagKey<T>.Create(_registry.Key, tagId.Value);
            HolderSet<T> set = _registry.GetOrCreate(tag);
            return DataResult<HolderSet<T>>.Success(set);
        }
        return DataResult<HolderSet<T>>.Success(new DirectHolderSet<T>(new[] { Resolve(text) }));
    }

    //Resolve 按注册名取已注册 Holder 缺注册项时退化成未绑定 Reference 保留注册名
    private Holder<T> Resolve(string text)
    {
        var id = Identifier.Parse(text);
        return _registry.Get(id)
            ?? Reference<T>.CreateStandAlone((HolderOwner<T>)_registry, ResourceKey<T>.Create(_registry.Key, id));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, HolderSet<T> value)
    {
        var tag = value.UnwrapKey();
        if (tag is not null)
            return DataResult<U>.Success(ops.CreateString("#" + tag.Location));
        var list = new List<U>();
        foreach (var holder in value)
        {
            var key = holder.UnwrapKey();
            if (key is not null) list.Add(ops.CreateString(key.Identifier.ToString()));
        }
        return DataResult<U>.Success(ops.CreateList(list));
    }
}
