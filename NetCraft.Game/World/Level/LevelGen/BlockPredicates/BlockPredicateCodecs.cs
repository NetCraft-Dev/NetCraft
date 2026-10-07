using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//Vec3iCodec int vector codec, maps to vanilla Vec3i.CODEC and Vec3i.offsetCodec(16)
//JSON form is three ints [x, y, z]; when the per-axis cap is non-zero it validates the bounds
internal sealed class Vec3iCodec : ScalarCodec<Vec3i>
{
    public static readonly Vec3iCodec Offset16 = new(16);
    public static readonly Vec3iCodec Unbounded = new(0);

    private readonly int _maxOffsetPerAxis;

    public Vec3iCodec(int maxOffsetPerAxis) => _maxOffsetPerAxis = maxOffsetPerAxis;

    public override DataResult<Vec3i> Parse<U>(DynamicOps<U> ops, U input)
    {
        var streamResult = ops.GetStream(input);
        if (!streamResult.Result().IsPresent)
            return DataResult<Vec3i>.Error(() => "offset must be an [x, y, z] array");
        var components = new List<int>();
        foreach (var element in streamResult.GetOrThrow())
        {
            var numberResult = ops.GetNumberValue(element);
            if (!numberResult.Result().IsPresent)
                return DataResult<Vec3i>.Error(() => "offset components must be integers");
            components.Add((int)numberResult.GetOrThrow());
        }
        if (components.Count != 3)
            return DataResult<Vec3i>.Error(() => $"offset needs 3 components, got {components.Count}");
        var offset = new Vec3i(components[0], components[1], components[2]);
        if (_maxOffsetPerAxis > 0
            && (Math.Abs(offset.X) >= _maxOffsetPerAxis || Math.Abs(offset.Y) >= _maxOffsetPerAxis
                || Math.Abs(offset.Z) >= _maxOffsetPerAxis))
            return DataResult<Vec3i>.Error(() => $"offset out of range, at most {_maxOffsetPerAxis} per axis: {offset}");
        return DataResult<Vec3i>.Success(offset);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Vec3i value)
        => DataResult<U>.Success(ops.CreateIntList(new[] { value.X, value.Y, value.Z }));
}

//DirectionCodec direction codec, maps to vanilla Direction.CODEC
//JSON form is a lowercase name: down/up/north/south/west/east
internal sealed class DirectionCodec : ScalarCodec<Direction>
{
    public static readonly DirectionCodec Instance = new();

    private static readonly string[] Names = { "down", "up", "north", "south", "west", "east" };

    public override DataResult<Direction> Parse<U>(DynamicOps<U> ops, U input)
    {
        var textResult = ops.GetStringValue(input);
        if (!textResult.Result().IsPresent)
            return DataResult<Direction>.Error(() => "direction must be a string");
        var name = textResult.GetOrThrow();
        for (var i = 0; i < Names.Length; i++)
            if (Names[i] == name) return DataResult<Direction>.Success(Direction.Values[i]);
        return DataResult<Direction>.Error(() => $"unknown direction: {name}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Direction value)
        => DataResult<U>.Success(ops.CreateString(Names[value.Id3D]));
}

//TagKeyCodec tag key codec, maps to vanilla TagKey.codec
//JSON form is a namespace:path string without a # prefix (the # form is TagKey.hashedCodec)
internal sealed class TagKeyCodec<T> : ScalarCodec<TagKey<T>> where T : class
{
    private readonly Registry<T> _registry;

    public TagKeyCodec(Registry<T> registry) => _registry = registry;

    public override DataResult<TagKey<T>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var textResult = ops.GetStringValue(input);
        if (!textResult.Result().IsPresent)
            return DataResult<TagKey<T>>.Error(() => "tag must be a string");
        var id = Identifier.TryParse(textResult.GetOrThrow());
        return id is null
            ? DataResult<TagKey<T>>.Error(() => $"invalid tag id: {textResult.GetOrThrow()}")
            : DataResult<TagKey<T>>.Success(TagKey<T>.Create(_registry.Key, id.Value));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, TagKey<T> value)
        => DataResult<U>.Success(ops.CreateString(value.Location.ToString()));
}

//RegistryHolderSetCodec registry element set codec, maps to vanilla RegistryCodecs.homogeneousList
//Accepts a single id string or an array of id strings; a leading # is treated as a tag reference
//NetCraft's HolderSetCodec is internal to the Registry assembly and unavailable in Game, so this is a copy for biomes and fluids
internal sealed class RegistryHolderSetCodec<T> : ScalarCodec<HolderSet<T>> where T : class
{
    private readonly Registry<T> _registry;

    public RegistryHolderSetCodec(Registry<T> registry) => _registry = registry;

    public override DataResult<HolderSet<T>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent) return ParseOne(text.GetOrThrow());
        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent)
            return DataResult<HolderSet<T>>.Error(() => "set must be a string or an array of strings");
        var holders = new List<Holder<T>>();
        foreach (var element in stream.GetOrThrow())
        {
            var elementText = ops.GetStringValue(element);
            if (!elementText.Result().IsPresent)
                return DataResult<HolderSet<T>>.Error(() => "set elements must be strings");
            holders.Add(Resolve(elementText.GetOrThrow()));
        }
        return DataResult<HolderSet<T>>.Success(new DirectHolderSet<T>(holders));
    }

    //ParseOne a single string starting with # decodes to a tag set, otherwise to a single-element set
    private DataResult<HolderSet<T>> ParseOne(string text)
    {
        if (text.StartsWith('#'))
        {
            var tagId = Identifier.TryParse(text[1..]);
            if (tagId is null)
                return DataResult<HolderSet<T>>.Error(() => $"invalid tag id: {text}");
            var tag = TagKey<T>.Create(_registry.Key, tagId.Value);
            HolderSet<T> set = _registry.GetOrCreate(tag);
            return DataResult<HolderSet<T>>.Success(set);
        }
        return DataResult<HolderSet<T>>.Success(new DirectHolderSet<T>(new[] { Resolve(text) }));
    }

    //Resolve look up a registered Holder by name; an unregistered one degrades to an unbound Reference
    private Holder<T> Resolve(string text)
    {
        var id = Identifier.Parse(text);
        return _registry.Get(id)
            ?? Reference<T>.CreateStandAlone((HolderOwner<T>)_registry, ResourceKey<T>.Create(_registry.Key, id));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, HolderSet<T> value)
    {
        var tag = value.UnwrapKey();
        if (tag is not null) return DataResult<U>.Success(ops.CreateString("#" + tag.Location));
        var list = new List<U>();
        foreach (var holder in value)
        {
            var key = holder.UnwrapKey();
            if (key is not null) list.Add(ops.CreateString(key.Identifier.ToString()));
        }
        return DataResult<U>.Success(ops.CreateList(list));
    }
}

//SingleFieldMapCodec single-field map codec, maps to the single-field form of vanilla RecordCodecBuilder
//The project's RecordCodecBuilder starts at two fields, so single-field predicates wrap with this
internal sealed class SingleFieldMapCodec<T, F> : AbstractMapCodec<T>
{
    private readonly MapCodec<F> _field;
    private readonly Func<F, T> _ctor;
    private readonly Func<T, F> _getter;

    public SingleFieldMapCodec(MapCodec<F> field, Func<F, T> ctor, Func<T, F> getter)
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

//UnitMapCodec parameterless predicate codec, maps to vanilla MapCodec.unit
internal sealed class UnitMapCodec<T> : AbstractMapCodec<T>
{
    private readonly Func<T> _factory;

    public UnitMapCodec(Func<T> factory) => _factory = factory;

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<T>.Success(_factory());

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder) => builder;
}
