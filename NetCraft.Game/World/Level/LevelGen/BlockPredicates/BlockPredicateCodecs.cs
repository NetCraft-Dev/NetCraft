using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//Vec3iCodec 整型向量编解码对应原版 Vec3i.CODEC 与 Vec3i.offsetCodec(16)
//JSON 形态是 [x, y, z] 三个整数 每轴上限非零时做越界校验
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
            return DataResult<Vec3i>.Error(() => "偏移必须是 [x, y, z] 数组");
        var components = new List<int>();
        foreach (var element in streamResult.GetOrThrow())
        {
            var numberResult = ops.GetNumberValue(element);
            if (!numberResult.Result().IsPresent)
                return DataResult<Vec3i>.Error(() => "偏移分量必须是整数");
            components.Add((int)numberResult.GetOrThrow());
        }
        if (components.Count != 3)
            return DataResult<Vec3i>.Error(() => $"偏移需要 3 个分量 实际 {components.Count}");
        var offset = new Vec3i(components[0], components[1], components[2]);
        if (_maxOffsetPerAxis > 0
            && (Math.Abs(offset.X) >= _maxOffsetPerAxis || Math.Abs(offset.Y) >= _maxOffsetPerAxis
                || Math.Abs(offset.Z) >= _maxOffsetPerAxis))
            return DataResult<Vec3i>.Error(() => $"偏移越界 每轴最多 {_maxOffsetPerAxis}: {offset}");
        return DataResult<Vec3i>.Success(offset);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Vec3i value)
        => DataResult<U>.Success(ops.CreateIntList(new[] { value.X, value.Y, value.Z }));
}

//DirectionCodec 方向编解码对应原版 Direction.CODEC
//JSON 形态是 down/up/north/south/west/east 小写名
internal sealed class DirectionCodec : ScalarCodec<Direction>
{
    public static readonly DirectionCodec Instance = new();

    private static readonly string[] Names = { "down", "up", "north", "south", "west", "east" };

    public override DataResult<Direction> Parse<U>(DynamicOps<U> ops, U input)
    {
        var textResult = ops.GetStringValue(input);
        if (!textResult.Result().IsPresent)
            return DataResult<Direction>.Error(() => "方向必须是字符串");
        var name = textResult.GetOrThrow();
        for (var i = 0; i < Names.Length; i++)
            if (Names[i] == name) return DataResult<Direction>.Success(Direction.Values[i]);
        return DataResult<Direction>.Error(() => $"未知方向: {name}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Direction value)
        => DataResult<U>.Success(ops.CreateString(Names[value.Id3D]));
}

//TagKeyCodec 标签键编解码对应原版 TagKey.codec
//JSON 形态是 namespace:path 字符串 不带 # 前缀(带 # 的是 TagKey.hashedCodec 形态)
internal sealed class TagKeyCodec<T> : ScalarCodec<TagKey<T>> where T : class
{
    private readonly Registry<T> _registry;

    public TagKeyCodec(Registry<T> registry) => _registry = registry;

    public override DataResult<TagKey<T>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var textResult = ops.GetStringValue(input);
        if (!textResult.Result().IsPresent)
            return DataResult<TagKey<T>>.Error(() => "标签必须是字符串");
        var id = Identifier.TryParse(textResult.GetOrThrow());
        return id is null
            ? DataResult<TagKey<T>>.Error(() => $"非法标签 id: {textResult.GetOrThrow()}")
            : DataResult<TagKey<T>>.Success(TagKey<T>.Create(_registry.Key, id.Value));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, TagKey<T> value)
        => DataResult<U>.Success(ops.CreateString(value.Location.ToString()));
}

//RegistryHolderSetCodec 注册表元素集合编解码对应原版 RegistryCodecs.homogeneousList
//单个 id 字符串或 id 字符串数组都接受 前缀 # 视为标签引用
//NetCraft 的 HolderSetCodec 是 Registry 程序集内部类 Game 层用不了 这里复制一份供群系与流体使用
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
            return DataResult<HolderSet<T>>.Error(() => "集合必须是字符串或字符串数组");
        var holders = new List<Holder<T>>();
        foreach (var element in stream.GetOrThrow())
        {
            var elementText = ops.GetStringValue(element);
            if (!elementText.Result().IsPresent)
                return DataResult<HolderSet<T>>.Error(() => "集合元素必须是字符串");
            holders.Add(Resolve(elementText.GetOrThrow()));
        }
        return DataResult<HolderSet<T>>.Success(new DirectHolderSet<T>(holders));
    }

    //ParseOne 单字符串 # 前缀解成标签集合 否则解成单元素集合
    private DataResult<HolderSet<T>> ParseOne(string text)
    {
        if (text.StartsWith('#'))
        {
            var tagId = Identifier.TryParse(text[1..]);
            if (tagId is null)
                return DataResult<HolderSet<T>>.Error(() => $"非法标签 id: {text}");
            var tag = TagKey<T>.Create(_registry.Key, tagId.Value);
            HolderSet<T> set = _registry.GetOrCreate(tag);
            return DataResult<HolderSet<T>>.Success(set);
        }
        return DataResult<HolderSet<T>>.Success(new DirectHolderSet<T>(new[] { Resolve(text) }));
    }

    //Resolve 按注册名取已注册 Holder 未注册退化成未绑定 Reference
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

//SingleFieldMapCodec 单字段 map codec 对应原版 RecordCodecBuilder 单字段形态
//项目 RecordCodecBuilder 从两字段起 单字段谓词用这个包装
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

//UnitMapCodec 无参数谓词编解码对应原版 MapCodec.unit
internal sealed class UnitMapCodec<T> : AbstractMapCodec<T>
{
    private readonly Func<T> _factory;

    public UnitMapCodec(Func<T> factory) => _factory = factory;

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<T>.Success(_factory());

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder) => builder;
}
