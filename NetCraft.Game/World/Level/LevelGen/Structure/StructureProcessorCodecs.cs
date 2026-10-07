using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureProcessorType processor single-element codec entry point, maps to vanilla StructureProcessorType.SINGLE_CODEC
//Looks up STRUCTURE_PROCESSOR by the processor_type field then hands off to that type's codec
public static class StructureProcessorType
{
    public static readonly Codec<StructureProcessor> SingleCodec = new StructureProcessorDispatchCodec();
}

//StructureProcessorTypes processor type registration, maps to vanilla StructureProcessorTypes.bootstrap
public static class StructureProcessorTypes
{
    //RegisterAll fills the 11 processor types into STRUCTURE_PROCESSOR under the vanilla registry names
    public static void RegisterAll()
    {
        Register("nop", NopProcessor.MapCodec);
        Register("block_ignore", BlockIgnoreProcessor.MapCodec);
        Register("block_rot", BlockRotProcessor.MapCodec);
        Register("gravity", GravityProcessor.MapCodec);
        Register("jigsaw_replacement", JigsawReplacementProcessor.MapCodec);
        Register("protected_blocks", ProtectedBlockProcessor.MapCodec);
        Register("block_age", BlockAgeProcessor.MapCodec);
        Register("blackstone_replace", BlackstoneReplaceProcessor.MapCodec);
        Register("lava_submerged_block", LavaSubmergedBlockProcessor.MapCodec);
        Register("capped", CappedProcessor.MapCodec);
        Register("rule", RuleProcessor.MapCodec);
    }

    //Register the registry element type is the registry-side marker interface, so a strongly typed codec is wrapped first
    private static void Register<T>(string path, MapCodec<T> codec) where T : class, StructureProcessor
        => Registry<MapCodec<NetCraft.Registry.StructureProcessor>>.Register(
            BuiltInRegistries.STRUCTURE_PROCESSOR, path,
            new ProcessorMapCodec<T, NetCraft.Registry.StructureProcessor>(codec));
}

//StructureProcessorDispatchCodec processor polymorphic codec, maps to the dispatch of vanilla SINGLE_CODEC
internal sealed class StructureProcessorDispatchCodec : ScalarCodec<StructureProcessor>
{
    public override DataResult<StructureProcessor> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeProcessor(ops, map));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, StructureProcessor value)
        => DataResult<U>.Error(() => "processor encoding not implemented yet");

    //DecodeProcessor reads processor_type, looks it up then decodes, reused by processor_list and processors internally
    internal static DataResult<StructureProcessor> DecodeProcessor<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("processor_type");
        if (!typeTag.IsPresent) return DataResult<StructureProcessor>.Error(() => "processor is missing processor_type");
        var text = ops.GetStringValue(typeTag.Get());
        if (!text.Result().IsPresent) return DataResult<StructureProcessor>.Error(() => "processor_type must be a string");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null) return DataResult<StructureProcessor>.Error(() => $"invalid processor type: {text.GetOrThrow()}");
        if (!BuiltInRegistries.STRUCTURE_PROCESSOR.ContainsKey(id.Value))
            return DataResult<StructureProcessor>.Error(() => $"unregistered processor type: {id}");
        var codec = BuiltInRegistries.STRUCTURE_PROCESSOR.GetValue(id.Value)!;
        var decoded = codec.Decode(ops, input);
        if (!decoded.Result().IsPresent) return DataResult<StructureProcessor>.Error(() => $"failed to parse processor {id}");
        var value = decoded.GetOrThrow();
        if (value is not StructureProcessor processor)
            return DataResult<StructureProcessor>.Error(() => $"processor {id} returned the wrong result type");
        return DataResult<StructureProcessor>.Success(processor);
    }
}

//ProcessorMapCodec adapts a strongly typed processor codec to the target element type, matching vanilla's registry element codec type erasure
//Vanilla MapCodec<? extends StructureProcessor> has no variance in C#, so a converting wrapper is the only way
internal sealed class ProcessorMapCodec<TSource, TTarget> : MapCodec<TTarget>
    where TSource : class, StructureProcessor
    where TTarget : class
{
    private readonly MapCodec<TSource> _inner;

    public ProcessorMapCodec(MapCodec<TSource> inner) => _inner = inner;

    public DataResult<TTarget> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _inner.Decode(ops, input).Map(v => (TTarget)(object)v);

    public DataResult<U> EncodeStart<U>(DynamicOps<U> ops, TTarget value)
        => value is TSource source
            ? _inner.EncodeStart(ops, source)
            : DataResult<U>.Error(() => $"processor codec does not match the element type: {value.GetType().Name}");

    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, TTarget value, RecordBuilder<U> builder)
        => value is TSource source
            ? _inner.EncodeTo(ops, source, builder)
            : throw new InvalidOperationException($"processor codec does not match the element type: {value.GetType().Name}");

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops) => _inner.Encoder(ops);
}

//StructureUnitMapCodec fieldless codec, maps to vanilla MapCodec.unit
internal sealed class StructureUnitMapCodec<T> : AbstractMapCodec<T>
{
    private readonly Func<T> _factory;

    public StructureUnitMapCodec(Func<T> factory) => _factory = factory;

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<T>.Success(_factory());

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder) => builder;
}

//StructureSingleFieldMapCodec single-field codec, matches the single-field shape of vanilla RecordCodecBuilder
//This project's RecordCodecBuilder starts at two fields, so single-field processor types are wrapped with this
internal sealed class StructureSingleFieldMapCodec<T, F> : AbstractMapCodec<T>
{
    private readonly MapCodec<F> _field;
    private readonly Func<F, T> _ctor;
    private readonly Func<T, F> _getter;

    public StructureSingleFieldMapCodec(MapCodec<F> field, Func<F, T> ctor, Func<T, F> getter)
    {
        _field = field;
        _ctor = ctor;
        _getter = getter;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _field.Decode(ops, input).Map(_ctor);

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
        => _field.EncodeTo(ops, _getter(value), builder);
}

//StructureAxisCodec axis codec, maps to vanilla Direction.Axis.CODEC
//The JSON form is a single lowercase x/y/z character
internal sealed class StructureAxisCodec : ScalarCodec<Direction.Axis>
{
    public static readonly StructureAxisCodec Instance = new();

    public override DataResult<Direction.Axis> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<Direction.Axis>.Error(() => "axis must be a string");
        return text.GetOrThrow() switch
        {
            "x" => DataResult<Direction.Axis>.Success(Direction.Axis.X),
            "y" => DataResult<Direction.Axis>.Success(Direction.Axis.Y),
            "z" => DataResult<Direction.Axis>.Success(Direction.Axis.Z),
            var other => DataResult<Direction.Axis>.Error(() => $"unknown axis: {other}")
        };
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Direction.Axis value)
        => DataResult<U>.Success(ops.CreateString(value switch
        {
            Direction.Axis.X => "x",
            Direction.Axis.Y => "y",
            _ => "z",
        }));
}

//StructureBlockCodec block reference codec, maps to vanilla BuiltInRegistries.BLOCK.byNameCodec
//The JSON form is the block registry name; BLOCK is a DefaultedRegistry so an unknown name falls back to air, so existence must be checked first
internal sealed class StructureBlockCodec : ScalarCodec<RegBlock>
{
    public static readonly StructureBlockCodec Instance = new();

    public override DataResult<RegBlock> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<RegBlock>.Error(() => "block must be a string");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null) return DataResult<RegBlock>.Error(() => $"invalid block name: {text.GetOrThrow()}");
        return BuiltInRegistries.BLOCK.ContainsKey(id.Value)
            ? DataResult<RegBlock>.Success(BuiltInRegistries.BLOCK.GetValue(id.Value)!)
            : DataResult<RegBlock>.Error(() => $"unknown block: {id}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, RegBlock value)
        => DataResult<U>.Success(ops.CreateString(value.Id.ToString()));
}

//StructureBlockTagCodec block tag codec, maps to vanilla TagKey.codec(Registries.BLOCK)
//The JSON form is a #namespace:path string
internal sealed class StructureBlockTagCodec : ScalarCodec<TagKey<RegBlock>>
{
    public static readonly StructureBlockTagCodec Instance = new();

    public override DataResult<TagKey<RegBlock>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<TagKey<RegBlock>>.Error(() => "tag must be a string");
        var raw = text.GetOrThrow();
        var body = raw.StartsWith('#') ? raw[1..] : raw;
        var id = Identifier.TryParse(body);
        return id is null
            ? DataResult<TagKey<RegBlock>>.Error(() => $"invalid tag name: {raw}")
            : DataResult<TagKey<RegBlock>>.Success(TagKey<RegBlock>.Create(Registries.BLOCK, id.Value));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, TagKey<RegBlock> value)
        => DataResult<U>.Success(ops.CreateString("#" + value.Location));
}

//ProcessorBlockHelper shared block lookup and property copying for processors
internal static class ProcessorBlockHelper
{
    //BlockOf fetches a block by registry name, returns null when the block table lacks it
    public static RegBlock? BlockOf(string path)
    {
        var id = Identifier.WithDefaultNamespace(path);
        return BuiltInRegistries.BLOCK.ContainsKey(id) ? BuiltInRegistries.BLOCK.GetValue(id) : null;
    }

    //StateOf fetches the default state by registry name
    public static BlockState StateOf(string path) => BlockOf(path)?.DefaultBlockState ?? default;

    //HasBlock whether the state is made of the given block
    public static bool HasBlock(BlockState state, string path) => state.Id != 0 && state.Owner.Id == Identifier.WithDefaultNamespace(path);

    //InTag whether the state belongs to a block tag; an unbound tag counts as not belonging
    public static bool InTag(BlockState state, TagKey<RegBlock> tag)
    {
        var set = BuiltInRegistries.BLOCK.Get(tag);
        if (set is null || !set.IsBound) return false;
        return set.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner));
    }

    //InSet whether the state belongs to a resolved block set
    public static bool InSet(BlockState state, HolderSet<RegBlock> set)
    {
        if (state.Id == 0 || !set.IsBound) return false;
        foreach (var holder in set)
        {
            if (holder.UnwrapKey() is not { } key) continue;
            if (key.Identifier == state.Owner.Id) return true;
        }
        return false;
    }

    //FindProperty looks up a property by name in the state's property table
    public static PropertyBase? FindProperty(BlockState state, string name)
    {
        foreach (var property in state.GetProperties())
            if (property.Name == name) return property;
        return null;
    }

    //CopyProperties copies each property from the source state to the target, writing only when both name and value are valid
    public static BlockState CopyProperties(BlockState from, BlockState to)
    {
        foreach (var entry in from.GetValues())
        {
            var target = FindProperty(to, entry.Property.Name);
            if (target is null) continue;
            to = StructureBlockTransforms.SetIfAllowed(to, target, entry.Value);
        }
        return to;
    }

    //ParseStateString parses a block state string like "minecraft:stone[facing=north]", returns null when invalid
    public static BlockState? ParseStateString(string text)
    {
        var bracketStart = text.IndexOf('[');
        var name = (bracketStart < 0 ? text : text[..bracketStart]).Trim();
        var id = Identifier.TryParse(name);
        if (id is null || !BuiltInRegistries.BLOCK.ContainsKey(id.Value)) return null;
        var tag = new CompoundTag();
        tag.PutString("Name", name);
        if (bracketStart >= 0)
        {
            var end = text.IndexOf(']', bracketStart);
            if (end < 0) return null;
            var properties = new CompoundTag();
            foreach (var pair in text[(bracketStart + 1)..end].Split(','))
            {
                var split = pair.Split('=', 2);
                if (split.Length != 2) continue;
                properties.PutString(split[0].Trim(), split[1].Trim());
            }
            tag.Put("Properties", properties);
        }
        return StructureTemplate.ReadBlockState(tag);
    }
}

//StructureHeightmapTypeCodec heightmap type codec, maps to vanilla Heightmap.Types.CODEC
internal sealed class StructureHeightmapTypeCodec : ScalarCodec<NetCraft.Registry.Heightmap.Types>
{
    public static readonly StructureHeightmapTypeCodec Instance = new();

    public override DataResult<NetCraft.Registry.Heightmap.Types> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<NetCraft.Registry.Heightmap.Types>.Error(() => "heightmap must be a string");
        var parsed = NetCraft.Registry.Heightmap.FromSerializationKey(text.GetOrThrow().ToUpperInvariant());
        return parsed is null
            ? DataResult<NetCraft.Registry.Heightmap.Types>.Error(() => $"unknown heightmap type: {text.GetOrThrow()}")
            : DataResult<NetCraft.Registry.Heightmap.Types>.Success(parsed.Value);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.Registry.Heightmap.Types value)
        => DataResult<U>.Success(ops.CreateString(value.GetSerializationKey().ToLowerInvariant()));
}

//StructureCompoundTagCodec compound tag codec, maps to vanilla CompoundTag.CODEC
//A native Tag passes through directly; a JSON-form map is converted value by value into tags; encoding supports native tags only
internal sealed class StructureCompoundTagCodec : ScalarCodec<CompoundTag>
{
    public static readonly StructureCompoundTagCodec Instance = new();

    public override DataResult<CompoundTag> Parse<U>(DynamicOps<U> ops, U input)
        => DataResult<CompoundTag>.Success(ProcessorNbtConversion.ToCompoundTag(ops, input));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, CompoundTag value)
        => value is U native ? DataResult<U>.Success(native) : DataResult<U>.Error(() => "compound tag encoding supports native tags only");
}

//ProcessorNbtConversion converts a JSON-form compound tag to CompoundTag, maps to the cross-ops conversion in vanilla CompoundTag.CODEC
internal static class ProcessorNbtConversion
{
    //ToCompoundTag converts a map-form value into CompoundTag, returning directly when it is already a native tag
    public static CompoundTag ToCompoundTag<U>(DynamicOps<U> ops, U input)
    {
        if (input is CompoundTag tag) return tag;
        var result = new CompoundTag();
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return result;
        foreach (var (keyTag, valueTag) in mapResult.GetOrThrow().Entries())
        {
            var key = ops.GetStringValue(keyTag);
            if (!key.Result().IsPresent) continue;
            result.Put(key.GetOrThrow(), ToTag(ops, valueTag));
        }
        return result;
    }

    //ToTag converts by JSON value shape layer by layer; numbers split into integer and floating point
    private static Tag ToTag<U>(DynamicOps<U> ops, U input)
    {
        if (input is Tag native) return native;
        if (ops.GetMap(input).Result().IsPresent) return ToCompoundTag(ops, input);
        var streamResult = ops.GetStream(input);
        if (streamResult.Result().IsPresent)
        {
            var list = new ListTag();
            foreach (var element in streamResult.GetOrThrow()) list.Add(ToTag(ops, element));
            return list;
        }
        if (ops.GetStringValue(input).Result().IsPresent)
            return new StringTag(ops.GetStringValue(input).GetOrThrow());
        var numberResult = ops.GetNumberValue(input);
        if (numberResult.Result().IsPresent)
        {
            var value = numberResult.GetOrThrow();
            return value == Math.Floor(value) && Math.Abs(value) < int.MaxValue
                ? new IntTag((int)value)
                : new DoubleTag(value);
        }
        var booleanResult = ops.GetBooleanValue(input);
        if (booleanResult.Result().IsPresent)
            return new ByteTag(booleanResult.GetOrThrow() ? (byte)1 : (byte)0);
        return new StringTag(input?.ToString() ?? "null");
    }
}
