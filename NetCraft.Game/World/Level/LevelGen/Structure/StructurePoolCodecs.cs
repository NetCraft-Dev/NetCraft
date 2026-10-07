using System.Runtime.CompilerServices;
using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using GamePlacedFeature = NetCraft.Game.World.Level.LevelGen.Placement.PlacedFeature;
using RegistryPlacedFeature = NetCraft.Registry.PlacedFeature;
using RegistryPool = NetCraft.Registry.StructureTemplatePool;
using RegistryProcessorList = NetCraft.Registry.StructureProcessorList;
//The RefCodecs in this file are all top level, so the static helpers are imported to avoid writing the class name everywhere
using static NetCraft.Game.World.Level.LevelGen.Structure.StructurePoolCodecs;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePoolCodecs shared codec pieces of the template pool subsystem, matching the inline codecs scattered around vanilla
internal static class StructurePoolCodecs
{
    //SourceTags original tags of objects parsed inline
    //The encoding paths for processor lists, placed features and template pools are not implemented yet, so the original tags are recorded during parsing
    //They are written back verbatim when the chunk unloads and saves; otherwise inline-form structures would crash the server main loop
    private static readonly ConditionalWeakTable<object, Tag> SourceTags = new();

    internal static void RememberSourceTag(object value, Tag tag) => SourceTags.AddOrUpdate(value, tag);

    internal static Tag? RecallSourceTag(object value)
        => SourceTags.TryGetValue(value, out var tag) ? tag : null;
    //TemplateLocation template reference codec; in vanilla this is the string side of an Either, and the runtime template form does not go through JSON
    public static readonly Codec<Identifier> TemplateLocation = new IdentifierScalarCodec();

    //LiquidSettingsCodec liquid handling codec, maps to vanilla LiquidSettings.CODEC
    public static readonly Codec<LiquidSettings> LiquidSettingsCodec = new LiquidSettingsScalarCodec();

    //ProjectionCodec projection codec, maps to vanilla StructureTemplatePool.Projection.CODEC
    public static readonly Codec<StructureTemplatePool.Projection> ProjectionCodec = new PoolProjectionScalarCodec();

    //ProcessorListRef processor list reference codec, maps to vanilla StructureProcessorType.LIST_CODEC
    public static readonly Codec<Holder<RegistryProcessorList>> ProcessorListRef = new ProcessorListRefCodec();

    //PlacedFeatureRef placed feature reference codec, used by feature_pool_element
    public static readonly Codec<Holder<RegistryPlacedFeature>> PlacedFeatureRef = new PlacedFeatureRefCodec();

    //TemplatePoolRef template pool reference codec, maps to vanilla RegistryFileCodec(TEMPLATE_POOL)
    public static readonly Codec<Holder<RegistryPool>> TemplatePoolRef = new StructureTemplatePoolRefCodec();
}

//IdentifierScalarCodec identifier codec, maps to vanilla Identifier.CODEC
internal sealed class IdentifierScalarCodec : ScalarCodec<Identifier>
{
    public override DataResult<Identifier> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<Identifier>.Error(() => "identifier must be a string");
        var id = Identifier.TryParse(text.GetOrThrow());
        return id is null
            ? DataResult<Identifier>.Error(() => $"invalid identifier: {text.GetOrThrow()}")
            : DataResult<Identifier>.Success(id.Value);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Identifier value)
        => DataResult<U>.Success(ops.CreateString(value.ToString()));
}

//LiquidSettingsScalarCodec liquid handling codec, maps to vanilla LiquidSettings.CODEC
internal sealed class LiquidSettingsScalarCodec : ScalarCodec<LiquidSettings>
{
    public override DataResult<LiquidSettings> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<LiquidSettings>.Error(() => "override_liquid_settings must be a string");
        return text.GetOrThrow() switch
        {
            "apply_waterlogging" => DataResult<LiquidSettings>.Success(LiquidSettings.ApplyWaterlogging),
            "ignore_waterlogging" => DataResult<LiquidSettings>.Success(LiquidSettings.IgnoreWaterlogging),
            var other => DataResult<LiquidSettings>.Error(() => $"unknown liquid handling: {other}")
        };
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, LiquidSettings value)
        => DataResult<U>.Success(ops.CreateString(
            value == LiquidSettings.ApplyWaterlogging ? "apply_waterlogging" : "ignore_waterlogging"));
}

//PoolProjectionScalarCodec projection codec, maps to vanilla Projection.CODEC
internal sealed class PoolProjectionScalarCodec : ScalarCodec<StructureTemplatePool.Projection>
{
    public override DataResult<StructureTemplatePool.Projection> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<StructureTemplatePool.Projection>.Error(() => "projection must be a string");
        var parsed = PoolProjections.TryParse(text.GetOrThrow());
        return parsed is null
            ? DataResult<StructureTemplatePool.Projection>.Error(() => $"unknown projection: {text.GetOrThrow()}")
            : DataResult<StructureTemplatePool.Projection>.Success(parsed.Value);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, StructureTemplatePool.Projection value)
        => DataResult<U>.Success(ops.CreateString(PoolProjections.Name(value)));
}

//ProcessorListRefCodec processor list reference codec, handles vanilla RegistryFileCodec's reference and inline forms
internal sealed class ProcessorListRefCodec : ScalarCodec<Holder<RegistryProcessorList>>
{
    public override DataResult<Holder<RegistryProcessorList>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent)
        {
            var id = Identifier.TryParse(text.GetOrThrow());
            if (id is null) return DataResult<Holder<RegistryProcessorList>>.Error(() => $"invalid processor list name: {text.GetOrThrow()}");
            var holder = BuiltInRegistries.PROCESSOR_LIST.Get(id.Value);
            return holder is null
                ? DataResult<Holder<RegistryProcessorList>>.Error(() => $"processor list {id} is not loaded yet")
                : DataResult<Holder<RegistryProcessorList>>.Success(holder);
        }

        var inline = StructureProcessorList.ElementCodec.Parse(ops, input);
        if (inline.Result().IsPresent)
        {
            var parsed = inline.GetOrThrow();
            //The inline form has no encoding implementation here yet; the original tag is recorded during parsing to be written back verbatim on chunk save
            RememberSourceTag(parsed, ops.ConvertTo(NbtOps.Instance, input));
            return DataResult<Holder<RegistryProcessorList>>.Success(Holder<RegistryProcessorList>.Direct(parsed));
        }
        return DataResult<Holder<RegistryProcessorList>>.Error(() => "processors is neither a reference name nor a processor list object");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<RegistryProcessorList> value)
    {
        var key = value.UnwrapKey();
        if (key is not null)
            return DataResult<U>.Success(ops.CreateString(key.Identifier.ToString()));

        //A missing registry name means the list was inlined during parsing; it is written back verbatim using the tag recorded then
        //The processor list encoding path is not implemented; accepting only a registry name or encoding as an object would throw on chunk unload and take down the main loop
        if (RecallSourceTag(value.Value) is { } tag)
            return DataResult<U>.Success(NbtOps.Instance.ConvertTo(ops, tag));
        return DataResult<U>.Error(() => "inline processor list is missing its original tag, cannot encode");
    }
}

//PlacedFeatureRefCodec placed feature reference codec, maps to vanilla RegistryFileCodec(PLACED_FEATURE)
internal sealed class PlacedFeatureRefCodec : ScalarCodec<Holder<RegistryPlacedFeature>>
{
    public override DataResult<Holder<RegistryPlacedFeature>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent)
        {
            var id = Identifier.TryParse(text.GetOrThrow());
            if (id is null) return DataResult<Holder<RegistryPlacedFeature>>.Error(() => $"invalid placed feature name: {text.GetOrThrow()}");
            var holder = BuiltInRegistries.PLACED_FEATURE.Get(id.Value);
            return holder is null
                ? DataResult<Holder<RegistryPlacedFeature>>.Error(() => $"placed feature {id} is not loaded yet")
                : DataResult<Holder<RegistryPlacedFeature>>.Success(holder);
        }

        var inline = GamePlacedFeature.ElementCodec.Parse(ops, input);
        if (inline.Result().IsPresent)
        {
            var parsed = inline.GetOrThrow();
            //The inline form has no encoding implementation here yet; the original tag is recorded during parsing to be written back verbatim on chunk save
            RememberSourceTag(parsed, ops.ConvertTo(NbtOps.Instance, input));
            return DataResult<Holder<RegistryPlacedFeature>>.Success(Holder<RegistryPlacedFeature>.Direct(parsed));
        }
        return DataResult<Holder<RegistryPlacedFeature>>.Error(() => "feature is neither a reference name nor a placed feature object");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<RegistryPlacedFeature> value)
    {
        var key = value.UnwrapKey();
        if (key is not null)
            return DataResult<U>.Success(ops.CreateString(key.Identifier.ToString()));

        //An inlined placed feature is written back verbatim using the tag recorded during parsing, for the same reason as processor lists
        if (RecallSourceTag(value.Value) is { } tag)
            return DataResult<U>.Success(NbtOps.Instance.ConvertTo(ops, tag));
        return DataResult<U>.Error(() => "inline placed feature is missing its original tag, cannot encode");
    }
}

//StructureTemplatePoolRefCodec template pool reference codec, maps to vanilla RegistryFileCodec(TEMPLATE_POOL)
internal sealed class StructureTemplatePoolRefCodec : ScalarCodec<Holder<RegistryPool>>
{
    public override DataResult<Holder<RegistryPool>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent)
        {
            var id = Identifier.TryParse(text.GetOrThrow());
            if (id is null) return DataResult<Holder<RegistryPool>>.Error(() => $"invalid template pool name: {text.GetOrThrow()}");
            var holder = BuiltInRegistries.TEMPLATE_POOL.Get(id.Value);
            //A pool can reference itself or a pool loaded later; vanilla defers binding via a registry lookup, so here the holder resolves on access
            if (holder is not null) return DataResult<Holder<RegistryPool>>.Success(holder);
            return DataResult<Holder<RegistryPool>>.Success(new StructurePoolReferenceHolder(id.Value));
        }

        var inline = StructureTemplatePool.DirectCodec.Parse(ops, input);
        if (inline.Result().IsPresent)
        {
            var parsed = inline.GetOrThrow();
            //The inline form has no encoding implementation here yet; the original tag is recorded during parsing to be written back verbatim on chunk save
            RememberSourceTag(parsed, ops.ConvertTo(NbtOps.Instance, input));
            return DataResult<Holder<RegistryPool>>.Success(Holder<RegistryPool>.Direct(parsed));
        }
        return DataResult<Holder<RegistryPool>>.Error(() => "fallback is neither a reference name nor a template pool object");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<RegistryPool> value)
    {
        var key = value.UnwrapKey();
        if (key is not null)
            return DataResult<U>.Success(ops.CreateString(key.Identifier.ToString()));

        //An inlined template pool is written back verbatim using the tag recorded during parsing, for the same reason as processor lists
        if (RecallSourceTag(value.Value) is { } tag)
            return DataResult<U>.Success(NbtOps.Instance.ConvertTo(ops, tag));
        return DataResult<U>.Error(() => "inline template pool is missing its original tag, cannot encode");
    }
}

//StructurePoolReferenceHolder reference holder for a template pool not loaded yet; it queries the registry on access
internal sealed class StructurePoolReferenceHolder : Holder<RegistryPool>
{
    private readonly ResourceKey<RegistryPool> _key;

    public StructurePoolReferenceHolder(Identifier id)
        => _key = ResourceKey<RegistryPool>.Create(BuiltInRegistries.TEMPLATE_POOL.Key, id);

    private RegistryPool? Resolved => BuiltInRegistries.TEMPLATE_POOL.GetValue(_key);

    public RegistryPool Value => Resolved ?? throw new InvalidOperationException($"template pool {_key.Identifier} is not loaded yet");

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
