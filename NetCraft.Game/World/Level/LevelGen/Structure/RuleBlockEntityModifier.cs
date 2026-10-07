using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//RuleBlockEntityModifier block entity data modifier of a processor rule, maps to vanilla RuleBlockEntityModifier
//After a rule hit it decides what block entity data the cell carries; null means none
public abstract class RuleBlockEntityModifier
{
    //Codec polymorphic entry, dispatches by type to a concrete type in the RULE_BLOCK_ENTITY_MODIFIER registry
    public static readonly Codec<RuleBlockEntityModifier> Codec = new RuleBlockEntityModifierDispatchCodec();

    //Apply produces the modified block entity data, maps to vanilla apply
    public abstract CompoundTag? Apply(RandomSource random, CompoundTag? existingTag);

    //Type the owning type singleton
    public abstract RuleBlockEntityModifierType Type { get; }
}

//RuleBlockEntityModifierType modifier type, maps to vanilla RuleBlockEntityModifierType
public abstract class RuleBlockEntityModifierType : NetCraft.Registry.RuleBlockEntityModifierType<object>
{
    public Identifier Id { get; }

    protected RuleBlockEntityModifierType(Identifier id) => Id = id;

    //DecodeModifier decodes a modifier from a map
    public abstract DataResult<RuleBlockEntityModifier> DecodeModifier<U>(DynamicOps<U> ops, MapLike<U> input);

    public override string ToString() => $"RuleBlockEntityModifierType[{Id}]";
}

//RuleBlockEntityModifierType<T> strongly typed modifier type
public sealed class RuleBlockEntityModifierType<T> : RuleBlockEntityModifierType where T : RuleBlockEntityModifier
{
    private readonly MapCodec<T> _codec;

    public RuleBlockEntityModifierType(Identifier id, MapCodec<T> codec) : base(id) => _codec = codec;

    public override DataResult<RuleBlockEntityModifier> DecodeModifier<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(v => (RuleBlockEntityModifier)v);
}

//RuleBlockEntityModifierTypes modifier type registration, maps to the static fields of vanilla RuleBlockEntityModifierType
public static class RuleBlockEntityModifierTypes
{
    public static readonly RuleBlockEntityModifierType<PassthroughModifier> Passthrough =
        Register("passthrough", PassthroughModifier.MapCodec);

    public static readonly RuleBlockEntityModifierType<ClearModifier> Clear =
        Register("clear", ClearModifier.MapCodec);

    public static readonly RuleBlockEntityModifierType<AppendStaticModifier> AppendStatic =
        Register("append_static", AppendStaticModifier.MapCodec);

    public static readonly RuleBlockEntityModifierType<AppendLootModifier> AppendLoot =
        Register("append_loot", AppendLootModifier.MapCodec);

    //Register registers into RULE_BLOCK_ENTITY_MODIFIER and returns the type instance
    private static RuleBlockEntityModifierType<T> Register<T>(string path, MapCodec<T> codec)
        where T : RuleBlockEntityModifier
    {
        var type = new RuleBlockEntityModifierType<T>(Identifier.WithDefaultNamespace(path), codec);
        Registry<NetCraft.Registry.RuleBlockEntityModifierType<object>>.Register(
            BuiltInRegistries.RULE_BLOCK_ENTITY_MODIFIER, path, type);
        return type;
    }
}

//PassthroughModifier passes data through unchanged, maps to vanilla Passthrough
public sealed class PassthroughModifier : RuleBlockEntityModifier
{
    public static readonly PassthroughModifier Instance = new();

    public static readonly MapCodec<PassthroughModifier> MapCodec =
        new StructureUnitMapCodec<PassthroughModifier>(() => Instance);

    private PassthroughModifier() { }

    public override CompoundTag? Apply(RandomSource random, CompoundTag? existingTag) => existingTag;

    public override RuleBlockEntityModifierType Type => RuleBlockEntityModifierTypes.Passthrough;
}

//ClearModifier clears the block entity data, maps to vanilla Clear
public sealed class ClearModifier : RuleBlockEntityModifier
{
    public static readonly ClearModifier Instance = new();

    public static readonly MapCodec<ClearModifier> MapCodec =
        new StructureUnitMapCodec<ClearModifier>(() => Instance);

    private ClearModifier() { }

    public override CompoundTag Apply(RandomSource random, CompoundTag? existingTag) => new();

    public override RuleBlockEntityModifierType Type => RuleBlockEntityModifierTypes.Clear;
}

//AppendStaticModifier merges fixed block entity data, maps to vanilla AppendStatic
public sealed class AppendStaticModifier : RuleBlockEntityModifier
{
    public static readonly MapCodec<AppendStaticModifier> MapCodec =
        new StructureSingleFieldMapCodec<AppendStaticModifier, CompoundTag>(
            StructureCompoundTagCodec.Instance.FieldOf("data"), tag => new AppendStaticModifier(tag), m => m.Data);

    public CompoundTag Data { get; }

    public AppendStaticModifier(CompoundTag data) => Data = data;

    public override CompoundTag Apply(RandomSource random, CompoundTag? existingTag)
    {
        if (existingTag is null) return (CompoundTag)Data.Copy();
        existingTag.Merge(Data);
        return existingTag;
    }

    public override RuleBlockEntityModifierType Type => RuleBlockEntityModifierTypes.AppendStatic;
}

//AppendLootModifier writes the loot table reference and random seed, maps to vanilla AppendLoot
//Writes only the LootTable and LootTableSeed keys; loot content is handled separately by the drop system
public sealed class AppendLootModifier : RuleBlockEntityModifier
{
    public static readonly MapCodec<AppendLootModifier> MapCodec =
        new StructureSingleFieldMapCodec<AppendLootModifier, Identifier>(
            IdentifierCodec.Instance.FieldOf("loot_table"), id => new AppendLootModifier(id), m => m.LootTable);

    public Identifier LootTable { get; }

    public AppendLootModifier(Identifier lootTable) => LootTable = lootTable;

    public override CompoundTag Apply(RandomSource random, CompoundTag? existingTag)
    {
        var result = existingTag is null ? new CompoundTag() : (CompoundTag)existingTag.Copy();
        result.PutString("LootTable", LootTable.ToString());
        result.PutLong("LootTableSeed", random.NextLong());
        return result;
    }

    public override RuleBlockEntityModifierType Type => RuleBlockEntityModifierTypes.AppendLoot;
}

//RuleBlockEntityModifierDispatchCodec modifier polymorphic codec, maps to the dispatch of vanilla RuleBlockEntityModifier.CODEC
internal sealed class RuleBlockEntityModifierDispatchCodec : ScalarCodec<RuleBlockEntityModifier>
{
    public override DataResult<RuleBlockEntityModifier> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeModifier(ops, map));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, RuleBlockEntityModifier value)
        => DataResult<U>.Error(() => "block entity modifier encoding not implemented yet");

    //DecodeModifier reads the type field, looks it up, then hands off to that type's codec
    internal static DataResult<RuleBlockEntityModifier> DecodeModifier<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<RuleBlockEntityModifier>.Error(() => "block entity modifier is missing type");
        var text = ops.GetStringValue(typeTag.Get());
        if (!text.Result().IsPresent) return DataResult<RuleBlockEntityModifier>.Error(() => "type must be a string");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null) return DataResult<RuleBlockEntityModifier>.Error(() => $"invalid modifier type: {text.GetOrThrow()}");
        if (!BuiltInRegistries.RULE_BLOCK_ENTITY_MODIFIER.ContainsKey(id.Value))
            return DataResult<RuleBlockEntityModifier>.Error(() => $"unregistered modifier type: {id}");
        var type = BuiltInRegistries.RULE_BLOCK_ENTITY_MODIFIER.GetValue(id.Value) as RuleBlockEntityModifierType;
        return type is null
            ? DataResult<RuleBlockEntityModifier>.Error(() => $"modifier type {id} cannot be parsed")
            : type.DecodeModifier(ops, input);
    }
}
