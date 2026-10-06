using NetCraft.Codec;

namespace NetCraft.Registry;

//HolderSetCodecs registry element set codec entry point, maps to vanilla RegistryCodecs.homogeneousList
//Accepts a single id string or an array of id strings; a # prefix is treated as a tag reference
public static class HolderSetCodecs
{
    //BlockSet block set codec used by a carver's replaceable field, bound to BuiltInRegistries.BLOCK
    public static readonly Codec<HolderSet<Block>> BlockSet = new HolderSetCodec<Block>(BuiltInRegistries.BLOCK);

    //ConfiguredCarverSet configured carver set codec used by a biome's carvers field
    public static readonly Codec<HolderSet<ConfiguredWorldCarver>> ConfiguredCarverSet =
        new HolderSetCodec<ConfiguredWorldCarver>(BuiltInRegistries.CONFIGURED_CARVER);

    //PlacedFeatureSet placed feature set codec used by a biome's features field
    public static readonly Codec<HolderSet<PlacedFeature>> PlacedFeatureSet =
        new HolderSetCodec<PlacedFeature>(BuiltInRegistries.PLACED_FEATURE);

    //ConfiguredFeatureRef single configured feature reference codec used by a placed_feature's feature field
    public static readonly Codec<Holder<ConfiguredFeature>> ConfiguredFeatureRef =
        new HolderRefCodec<ConfiguredFeature>(BuiltInRegistries.CONFIGURED_FEATURE);

    //BiomeSet biome set codec used by a structure's biomes field; supports #tags and id lists
    public static readonly Codec<HolderSet<Biome>> BiomeSet = new HolderSetCodec<Biome>(BuiltInRegistries.BIOME);

    //StructureRef single structure reference codec used by a structure set's structures field
    public static readonly Codec<Holder<Structure>> StructureRef =
        new HolderRefCodec<Structure>(BuiltInRegistries.STRUCTURE);

    //StructureSetRef single structure set reference codec used by the other_set field of placement exclusion zones
    public static readonly Codec<Holder<StructureSet>> StructureSetRef =
        new HolderRefCodec<StructureSet>(BuiltInRegistries.STRUCTURE_SET);

    //EntityTypeRef single entity type reference codec used by data carrying a type id, such as a bundle's saved bee
    public static readonly Codec<Holder<EntityType<object>>> EntityTypeRef =
        new HolderRefCodec<EntityType<object>>(BuiltInRegistries.ENTITY_TYPE);

    //EntityTypeSet entity type set codec used by an entity type predicate's types field
    public static readonly Codec<HolderSet<EntityType<object>>> EntityTypeSet =
        new HolderSetCodec<EntityType<object>>(BuiltInRegistries.ENTITY_TYPE);

    //BlockEntityTypeRef single block entity type reference codec used by block entity data carried on block items
    public static readonly Codec<Holder<BlockEntityType<object>>> BlockEntityTypeRef =
        new HolderRefCodec<BlockEntityType<object>>(BuiltInRegistries.BLOCK_ENTITY_TYPE);

    //ItemSet item set codec used by an item predicate's items field
    public static readonly Codec<HolderSet<Item>> ItemSet = new HolderSetCodec<Item>(BuiltInRegistries.ITEM);

    //VillagerTypeSet villager type set codec used by villager type predicate fields
    public static readonly Codec<HolderSet<VillagerType>> VillagerTypeSet =
        new HolderSetCodec<VillagerType>(BuiltInRegistries.VILLAGER_TYPE);

    //VillagerTypeRef single villager type reference codec used by villager variant components
    public static readonly Codec<Holder<VillagerType>> VillagerTypeRef =
        new HolderRefCodec<VillagerType>(BuiltInRegistries.VILLAGER_TYPE);

    //MobEffectRef single mob effect reference codec used by effect instances and potion contents
    public static readonly Codec<Holder<MobEffect>> MobEffectRef =
        new HolderRefCodec<MobEffect>(BuiltInRegistries.MOB_EFFECT);

    //PotionSet potion set codec used by potion predicate fields
    public static readonly Codec<HolderSet<Potion>> PotionSet =
        new HolderSetCodec<Potion>(BuiltInRegistries.POTION);

    //PotionRef single potion reference codec used by potion contents components
    public static readonly Codec<Holder<Potion>> PotionRef =
        new HolderRefCodec<Potion>(BuiltInRegistries.POTION);

    //TrimMaterialRef single trim material reference codec used by armor trim components
    public static readonly Codec<Holder<TrimMaterial>> TrimMaterialRef =
        new HolderRefCodec<TrimMaterial>(BuiltInRegistries.TRIM_MATERIAL);

    //TrimPatternRef single trim pattern reference codec used by armor trim components
    public static readonly Codec<Holder<TrimPattern>> TrimPatternRef =
        new HolderRefCodec<TrimPattern>(BuiltInRegistries.TRIM_PATTERN);

    //TrimMaterialSet trim material set codec used by trim predicate fields
    public static readonly Codec<HolderSet<TrimMaterial>> TrimMaterialSet =
        new HolderSetCodec<TrimMaterial>(BuiltInRegistries.TRIM_MATERIAL);

    //TrimPatternSet trim pattern set codec used by trim predicate fields
    public static readonly Codec<HolderSet<TrimPattern>> TrimPatternSet =
        new HolderSetCodec<TrimPattern>(BuiltInRegistries.TRIM_PATTERN);

    //JukeboxSongRef single jukebox song reference codec used by jukebox components
    public static readonly Codec<Holder<JukeboxSong>> JukeboxSongRef =
        new HolderRefCodec<JukeboxSong>(BuiltInRegistries.JUKEBOX_SONG);

    //JukeboxSongSet jukebox song set codec used by jukebox predicate fields
    public static readonly Codec<HolderSet<JukeboxSong>> JukeboxSongSet =
        new HolderSetCodec<JukeboxSong>(BuiltInRegistries.JUKEBOX_SONG);

    //EnchantmentSet enchantment set codec used by enchantment predicate fields
    public static readonly Codec<HolderSet<Enchantment>> EnchantmentSet =
        new HolderSetCodec<Enchantment>(BuiltInRegistries.ENCHANTMENT);

    //EnchantmentRef single enchantment reference codec used by enchantment table keys and single predicates
    public static readonly Codec<Holder<Enchantment>> EnchantmentRef =
        new HolderRefCodec<Enchantment>(BuiltInRegistries.ENCHANTMENT);

    //AttributeSet attribute set codec used by attribute modifier predicate fields
    public static readonly Codec<HolderSet<EntityAttribute.Attribute>> AttributeSet =
        new HolderSetCodec<EntityAttribute.Attribute>(BuiltInRegistries.ATTRIBUTE);

    //AttributeRef single attribute reference codec used by attribute modifier entries
    public static readonly Codec<Holder<EntityAttribute.Attribute>> AttributeRef =
        new HolderRefCodec<EntityAttribute.Attribute>(BuiltInRegistries.ATTRIBUTE);
}

//HolderRefCodec single-element registry reference codec, the string form of vanilla RegistryFileCodec
//The element must already be in the registry; an unregistered one errors out and is retried by the loader
//It cannot fall back to an unbound Reference: Register only finds an existing holder in _byKey and never reuses a detached instance, so the reference would break permanently
internal sealed class HolderRefCodec<T> : ScalarCodec<Holder<T>> where T : class
{
    private readonly Registry<T> _registry;

    public HolderRefCodec(Registry<T> registry) => _registry = registry;

    public override DataResult<Holder<T>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent)
            return DataResult<Holder<T>>.Error(() => "Registry element reference must be a string");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null)
            return DataResult<Holder<T>>.Error(() => $"Invalid identifier: {text.GetOrThrow()}");
        var holder = _registry.Get(id.Value);
        return holder is null
            ? DataResult<Holder<T>>.Error(() => $"Registry {_registry.Key.Identifier} does not have {id} yet")
            : DataResult<Holder<T>>.Success(holder);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<T> value)
    {
        var key = value.UnwrapKey();
        return key is null
            ? DataResult<U>.Error(() => "Direct holder has no registry name, cannot encode")
            : DataResult<U>.Success(ops.CreateString(key.Identifier.ToString()));
    }
}

//HolderSetCodec encodes/decodes a set of same-named elements bound to a given registry
//When the registry has not loaded the element yet it degrades to an unbound Reference keeping only the registry name
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

    //ParseOne single-string form; a # prefix decodes to a tag set, otherwise to a single-element direct set
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

    //Resolve gets a registered Holder by registry name; a missing entry degrades to an unbound Reference keeping the registry name
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
