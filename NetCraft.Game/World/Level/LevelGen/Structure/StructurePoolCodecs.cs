using System.Runtime.CompilerServices;
using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using GamePlacedFeature = NetCraft.Game.World.Level.LevelGen.Placement.PlacedFeature;
using RegistryPlacedFeature = NetCraft.Registry.PlacedFeature;
using RegistryPool = NetCraft.Registry.StructureTemplatePool;
using RegistryProcessorList = NetCraft.Registry.StructureProcessorList;
//同文件的 RefCodec 都在顶层 直接引静态辅助省得每处写类名
using static NetCraft.Game.World.Level.LevelGen.Structure.StructurePoolCodecs;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePoolCodecs 模板池子系统的公用 codec 片段 对应原版分散在各处的内联 codec
internal static class StructurePoolCodecs
{
    //SourceTags 内联解析出的对象的原始标签
    //处理器列表/已放置特征/模板池三类的编码路径还没实现 解析时把原始标签记下来
    //区块卸载存档时原样写回 否则内联形式的结构会让服务端主循环抛异常退出
    private static readonly ConditionalWeakTable<object, Tag> SourceTags = new();

    internal static void RememberSourceTag(object value, Tag tag) => SourceTags.AddOrUpdate(value, tag);

    internal static Tag? RecallSourceTag(object value)
        => SourceTags.TryGetValue(value, out var tag) ? tag : null;
    //TemplateLocation 模板引用 codec 原版是 Either 的字符串侧 运行时模板形态不走 JSON
    public static readonly Codec<Identifier> TemplateLocation = new IdentifierScalarCodec();

    //LiquidSettingsCodec 液体处理方式 codec 对应原版 LiquidSettings.CODEC
    public static readonly Codec<LiquidSettings> LiquidSettingsCodec = new LiquidSettingsScalarCodec();

    //ProjectionCodec 投影 codec 对应原版 StructureTemplatePool.Projection.CODEC
    public static readonly Codec<StructureTemplatePool.Projection> ProjectionCodec = new PoolProjectionScalarCodec();

    //ProcessorListRef 处理器列表引用 codec 对应原版 StructureProcessorType.LIST_CODEC
    public static readonly Codec<Holder<RegistryProcessorList>> ProcessorListRef = new ProcessorListRefCodec();

    //PlacedFeatureRef 已放置特征引用 codec 供 feature_pool_element 使用
    public static readonly Codec<Holder<RegistryPlacedFeature>> PlacedFeatureRef = new PlacedFeatureRefCodec();

    //TemplatePoolRef 模板池引用 codec 对应原版 RegistryFileCodec(TEMPLATE_POOL)
    public static readonly Codec<Holder<RegistryPool>> TemplatePoolRef = new StructureTemplatePoolRefCodec();
}

//IdentifierScalarCodec 标识符 codec 对应原版 Identifier.CODEC
internal sealed class IdentifierScalarCodec : ScalarCodec<Identifier>
{
    public override DataResult<Identifier> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<Identifier>.Error(() => "标识符必须是字符串");
        var id = Identifier.TryParse(text.GetOrThrow());
        return id is null
            ? DataResult<Identifier>.Error(() => $"非法的标识符: {text.GetOrThrow()}")
            : DataResult<Identifier>.Success(id.Value);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Identifier value)
        => DataResult<U>.Success(ops.CreateString(value.ToString()));
}

//LiquidSettingsScalarCodec 液体处理方式 codec 对应原版 LiquidSettings.CODEC
internal sealed class LiquidSettingsScalarCodec : ScalarCodec<LiquidSettings>
{
    public override DataResult<LiquidSettings> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<LiquidSettings>.Error(() => "override_liquid_settings 必须是字符串");
        return text.GetOrThrow() switch
        {
            "apply_waterlogging" => DataResult<LiquidSettings>.Success(LiquidSettings.ApplyWaterlogging),
            "ignore_waterlogging" => DataResult<LiquidSettings>.Success(LiquidSettings.IgnoreWaterlogging),
            var other => DataResult<LiquidSettings>.Error(() => $"未知的液体处理方式: {other}")
        };
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, LiquidSettings value)
        => DataResult<U>.Success(ops.CreateString(
            value == LiquidSettings.ApplyWaterlogging ? "apply_waterlogging" : "ignore_waterlogging"));
}

//PoolProjectionScalarCodec 投影 codec 对应原版 Projection.CODEC
internal sealed class PoolProjectionScalarCodec : ScalarCodec<StructureTemplatePool.Projection>
{
    public override DataResult<StructureTemplatePool.Projection> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<StructureTemplatePool.Projection>.Error(() => "projection 必须是字符串");
        var parsed = PoolProjections.TryParse(text.GetOrThrow());
        return parsed is null
            ? DataResult<StructureTemplatePool.Projection>.Error(() => $"未知的 projection: {text.GetOrThrow()}")
            : DataResult<StructureTemplatePool.Projection>.Success(parsed.Value);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, StructureTemplatePool.Projection value)
        => DataResult<U>.Success(ops.CreateString(PoolProjections.Name(value)));
}

//ProcessorListRefCodec 处理器列表引用 codec 对应原版 RegistryFileCodec 的引用与内联两种形态
internal sealed class ProcessorListRefCodec : ScalarCodec<Holder<RegistryProcessorList>>
{
    public override DataResult<Holder<RegistryProcessorList>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent)
        {
            var id = Identifier.TryParse(text.GetOrThrow());
            if (id is null) return DataResult<Holder<RegistryProcessorList>>.Error(() => $"非法的处理器列表名: {text.GetOrThrow()}");
            var holder = BuiltInRegistries.PROCESSOR_LIST.Get(id.Value);
            return holder is null
                ? DataResult<Holder<RegistryProcessorList>>.Error(() => $"处理器列表 {id} 还没有装载")
                : DataResult<Holder<RegistryProcessorList>>.Success(holder);
        }

        var inline = StructureProcessorList.ElementCodec.Parse(ops, input);
        if (inline.Result().IsPresent)
        {
            var parsed = inline.GetOrThrow();
            //内联形式在本作还没有编码实现 解析时记下原始标签供区块存档时原样写回
            RememberSourceTag(parsed, ops.ConvertTo(NbtOps.Instance, input));
            return DataResult<Holder<RegistryProcessorList>>.Success(Holder<RegistryProcessorList>.Direct(parsed));
        }
        return DataResult<Holder<RegistryProcessorList>>.Error(() => "processors 既不是引用名也不是处理器列表对象");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<RegistryProcessorList> value)
    {
        var key = value.UnwrapKey();
        if (key is not null)
            return DataResult<U>.Success(ops.CreateString(key.Identifier.ToString()));

        //没有注册名说明是解析时内联进来的列表 用解析时留下的原始标签原样写回
        //处理器列表的编码路径还没实现 只认注册名或按对象编码都会在区块卸载存档时抛异常掀翻主循环
        if (RecallSourceTag(value.Value) is { } tag)
            return DataResult<U>.Success(NbtOps.Instance.ConvertTo(ops, tag));
        return DataResult<U>.Error(() => "内联处理器列表缺少原始标签 无法编码");
    }
}

//PlacedFeatureRefCodec 已放置特征引用 codec 对应原版 RegistryFileCodec(PLACED_FEATURE)
internal sealed class PlacedFeatureRefCodec : ScalarCodec<Holder<RegistryPlacedFeature>>
{
    public override DataResult<Holder<RegistryPlacedFeature>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent)
        {
            var id = Identifier.TryParse(text.GetOrThrow());
            if (id is null) return DataResult<Holder<RegistryPlacedFeature>>.Error(() => $"非法的已放置特征名: {text.GetOrThrow()}");
            var holder = BuiltInRegistries.PLACED_FEATURE.Get(id.Value);
            return holder is null
                ? DataResult<Holder<RegistryPlacedFeature>>.Error(() => $"已放置特征 {id} 还没有装载")
                : DataResult<Holder<RegistryPlacedFeature>>.Success(holder);
        }

        var inline = GamePlacedFeature.ElementCodec.Parse(ops, input);
        if (inline.Result().IsPresent)
        {
            var parsed = inline.GetOrThrow();
            //内联形式在本作还没有编码实现 解析时记下原始标签供区块存档时原样写回
            RememberSourceTag(parsed, ops.ConvertTo(NbtOps.Instance, input));
            return DataResult<Holder<RegistryPlacedFeature>>.Success(Holder<RegistryPlacedFeature>.Direct(parsed));
        }
        return DataResult<Holder<RegistryPlacedFeature>>.Error(() => "feature 既不是引用名也不是已放置特征对象");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<RegistryPlacedFeature> value)
    {
        var key = value.UnwrapKey();
        if (key is not null)
            return DataResult<U>.Success(ops.CreateString(key.Identifier.ToString()));

        //内联进来的已放置特征用解析时留下的原始标签原样写回 理由同处理器列表
        if (RecallSourceTag(value.Value) is { } tag)
            return DataResult<U>.Success(NbtOps.Instance.ConvertTo(ops, tag));
        return DataResult<U>.Error(() => "内联已放置特征缺少原始标签 无法编码");
    }
}

//StructureTemplatePoolRefCodec 模板池引用 codec 对应原版 RegistryFileCodec(TEMPLATE_POOL)
internal sealed class StructureTemplatePoolRefCodec : ScalarCodec<Holder<RegistryPool>>
{
    public override DataResult<Holder<RegistryPool>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent)
        {
            var id = Identifier.TryParse(text.GetOrThrow());
            if (id is null) return DataResult<Holder<RegistryPool>>.Error(() => $"非法的模板池名: {text.GetOrThrow()}");
            var holder = BuiltInRegistries.TEMPLATE_POOL.Get(id.Value);
            //池可以引用自己或引用后装载的池 原版靠注册表 lookup 延迟绑定 这里给取值时再查的持有者
            if (holder is not null) return DataResult<Holder<RegistryPool>>.Success(holder);
            return DataResult<Holder<RegistryPool>>.Success(new StructurePoolReferenceHolder(id.Value));
        }

        var inline = StructureTemplatePool.DirectCodec.Parse(ops, input);
        if (inline.Result().IsPresent)
        {
            var parsed = inline.GetOrThrow();
            //内联形式在本作还没有编码实现 解析时记下原始标签供区块存档时原样写回
            RememberSourceTag(parsed, ops.ConvertTo(NbtOps.Instance, input));
            return DataResult<Holder<RegistryPool>>.Success(Holder<RegistryPool>.Direct(parsed));
        }
        return DataResult<Holder<RegistryPool>>.Error(() => "fallback 既不是引用名也不是模板池对象");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<RegistryPool> value)
    {
        var key = value.UnwrapKey();
        if (key is not null)
            return DataResult<U>.Success(ops.CreateString(key.Identifier.ToString()));

        //内联进来的模板池用解析时留下的原始标签原样写回 理由同处理器列表
        if (RecallSourceTag(value.Value) is { } tag)
            return DataResult<U>.Success(NbtOps.Instance.ConvertTo(ops, tag));
        return DataResult<U>.Error(() => "内联模板池缺少原始标签 无法编码");
    }
}

//StructurePoolReferenceHolder 还没装载的模板池的引用持有者 取值时才去注册表查
internal sealed class StructurePoolReferenceHolder : Holder<RegistryPool>
{
    private readonly ResourceKey<RegistryPool> _key;

    public StructurePoolReferenceHolder(Identifier id)
        => _key = ResourceKey<RegistryPool>.Create(BuiltInRegistries.TEMPLATE_POOL.Key, id);

    private RegistryPool? Resolved => BuiltInRegistries.TEMPLATE_POOL.GetValue(_key);

    public RegistryPool Value => Resolved ?? throw new InvalidOperationException($"模板池 {_key.Identifier} 还没有装载");

    public bool IsBound() => Resolved is not null;
    public bool AreComponentsBound() => false;
    public bool Is(Identifier key) => _key.Identifier == key;
    public bool Is(ResourceKey<RegistryPool> key) => _key.Identifier == key.Identifier;
    public bool Is(Predicate<ResourceKey<RegistryPool>> predicate) => predicate(_key);
    public bool Is(TagKey<RegistryPool> tag) => false;
    public Holder<RegistryPool>.Kind HolderKind => Holder<RegistryPool>.Kind.Direct;
    public DataComponentMap Components => DataComponentMap.Empty;
    public ResourceKey<RegistryPool>? UnwrapKey() => _key;
    public bool CanSerializeIn(HolderOwner<RegistryPool> owner) => true;
    public IEnumerable<TagKey<RegistryPool>> Tags() => Array.Empty<TagKey<RegistryPool>>();

    public override string ToString() => $"PoolReference{{{_key.Identifier}}}";
}
