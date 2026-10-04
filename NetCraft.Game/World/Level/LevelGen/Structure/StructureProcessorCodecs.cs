using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureProcessorType 处理器单元素 codec 入口 对应原版 StructureProcessorType.SINGLE_CODEC
//按 processor_type 字段查 STRUCTURE_PROCESSOR 再交给该类型的 codec
public static class StructureProcessorType
{
    public static readonly Codec<StructureProcessor> SingleCodec = new StructureProcessorDispatchCodec();
}

//StructureProcessorTypes 处理器类型登记 对应原版 StructureProcessorTypes.bootstrap
public static class StructureProcessorTypes
{
    //RegisterAll 按原版注册名把 11 个处理器类型填进 STRUCTURE_PROCESSOR
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

    //Register 注册表元素类型是注册表侧标记接口 强类型 codec 要先包一层适配
    private static void Register<T>(string path, MapCodec<T> codec) where T : class, StructureProcessor
        => Registry<MapCodec<NetCraft.Registry.StructureProcessor>>.Register(
            BuiltInRegistries.STRUCTURE_PROCESSOR, path,
            new ProcessorMapCodec<T, NetCraft.Registry.StructureProcessor>(codec));
}

//StructureProcessorDispatchCodec 处理器多态 codec 对应原版 SINGLE_CODEC 的 dispatch
internal sealed class StructureProcessorDispatchCodec : ScalarCodec<StructureProcessor>
{
    public override DataResult<StructureProcessor> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeProcessor(ops, map));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, StructureProcessor value)
        => DataResult<U>.Error(() => "处理器编码暂未实现");

    //DecodeProcessor 读 processor_type 查表再解 供 processor_list 与处理器内部复用
    internal static DataResult<StructureProcessor> DecodeProcessor<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("processor_type");
        if (!typeTag.IsPresent) return DataResult<StructureProcessor>.Error(() => "处理器缺少 processor_type");
        var text = ops.GetStringValue(typeTag.Get());
        if (!text.Result().IsPresent) return DataResult<StructureProcessor>.Error(() => "processor_type 必须是字符串");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null) return DataResult<StructureProcessor>.Error(() => $"非法的处理器类型: {text.GetOrThrow()}");
        if (!BuiltInRegistries.STRUCTURE_PROCESSOR.ContainsKey(id.Value))
            return DataResult<StructureProcessor>.Error(() => $"未注册的处理器类型: {id}");
        var codec = BuiltInRegistries.STRUCTURE_PROCESSOR.GetValue(id.Value)!;
        var decoded = codec.Decode(ops, input);
        if (!decoded.Result().IsPresent) return DataResult<StructureProcessor>.Error(() => $"处理器 {id} 解析失败");
        var value = decoded.GetOrThrow();
        if (value is not StructureProcessor processor)
            return DataResult<StructureProcessor>.Error(() => $"处理器 {id} 的结果类型不对");
        return DataResult<StructureProcessor>.Success(processor);
    }
}

//ProcessorMapCodec 把一个强类型处理器 codec 适配到目标元素类型 对应原版 registry 的元素 codec 泛型擦除
//原版 MapCodec<? extends StructureProcessor> 在 C# 里没有型变 只能靠转换包装
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
            : DataResult<U>.Error(() => $"处理器 codec 与元素类型不匹配: {value.GetType().Name}");

    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, TTarget value, RecordBuilder<U> builder)
        => value is TSource source
            ? _inner.EncodeTo(ops, source, builder)
            : throw new InvalidOperationException($"处理器 codec 与元素类型不匹配: {value.GetType().Name}");

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops) => _inner.Encoder(ops);
}

//StructureUnitMapCodec 无字段 codec 对应原版 MapCodec.unit
internal sealed class StructureUnitMapCodec<T> : AbstractMapCodec<T>
{
    private readonly Func<T> _factory;

    public StructureUnitMapCodec(Func<T> factory) => _factory = factory;

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<T>.Success(_factory());

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder) => builder;
}

//StructureSingleFieldMapCodec 单字段 codec 对应原版 RecordCodecBuilder 单字段形态
//项目 RecordCodecBuilder 从两字段起 单字段的处理器类型用这个包装
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

//StructureAxisCodec 轴编解码 对应原版 Direction.Axis.CODEC
//JSON 形态是 x/y/z 小写名单字符
internal sealed class StructureAxisCodec : ScalarCodec<Direction.Axis>
{
    public static readonly StructureAxisCodec Instance = new();

    public override DataResult<Direction.Axis> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<Direction.Axis>.Error(() => "axis 必须是字符串");
        return text.GetOrThrow() switch
        {
            "x" => DataResult<Direction.Axis>.Success(Direction.Axis.X),
            "y" => DataResult<Direction.Axis>.Success(Direction.Axis.Y),
            "z" => DataResult<Direction.Axis>.Success(Direction.Axis.Z),
            var other => DataResult<Direction.Axis>.Error(() => $"未知的 axis: {other}")
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

//StructureBlockCodec 方块引用编解码 对应原版 BuiltInRegistries.BLOCK.byNameCodec
//JSON 形态是方块注册名 BLOCK 是 DefaultedRegistry 未知名会回落空气 必须先查存在性
internal sealed class StructureBlockCodec : ScalarCodec<RegBlock>
{
    public static readonly StructureBlockCodec Instance = new();

    public override DataResult<RegBlock> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<RegBlock>.Error(() => "方块必须是字符串");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null) return DataResult<RegBlock>.Error(() => $"非法的方块名: {text.GetOrThrow()}");
        return BuiltInRegistries.BLOCK.ContainsKey(id.Value)
            ? DataResult<RegBlock>.Success(BuiltInRegistries.BLOCK.GetValue(id.Value)!)
            : DataResult<RegBlock>.Error(() => $"未知方块: {id}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, RegBlock value)
        => DataResult<U>.Success(ops.CreateString(value.Id.ToString()));
}

//StructureBlockTagCodec 方块标签编解码 对应原版 TagKey.codec(Registries.BLOCK)
//JSON 形态是 #namespace:path 字符串
internal sealed class StructureBlockTagCodec : ScalarCodec<TagKey<RegBlock>>
{
    public static readonly StructureBlockTagCodec Instance = new();

    public override DataResult<TagKey<RegBlock>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<TagKey<RegBlock>>.Error(() => "标签必须是字符串");
        var raw = text.GetOrThrow();
        var body = raw.StartsWith('#') ? raw[1..] : raw;
        var id = Identifier.TryParse(body);
        return id is null
            ? DataResult<TagKey<RegBlock>>.Error(() => $"非法的标签名: {raw}")
            : DataResult<TagKey<RegBlock>>.Success(TagKey<RegBlock>.Create(Registries.BLOCK, id.Value));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, TagKey<RegBlock> value)
        => DataResult<U>.Success(ops.CreateString("#" + value.Location));
}

//ProcessorBlockHelper 处理器共用的方块查表与属性搬运
internal static class ProcessorBlockHelper
{
    //BlockOf 按注册名取方块 方块表没有该方块时返回 null
    public static RegBlock? BlockOf(string path)
    {
        var id = Identifier.WithDefaultNamespace(path);
        return BuiltInRegistries.BLOCK.ContainsKey(id) ? BuiltInRegistries.BLOCK.GetValue(id) : null;
    }

    //StateOf 按注册名取默认状态
    public static BlockState StateOf(string path) => BlockOf(path)?.DefaultBlockState ?? default;

    //HasBlock 该状态是否由指定方块构成
    public static bool HasBlock(BlockState state, string path) => state.Id != 0 && state.Owner.Id == Identifier.WithDefaultNamespace(path);

    //InTag 该状态是否属于某方块标签 标签未绑定按不属于处理
    public static bool InTag(BlockState state, TagKey<RegBlock> tag)
    {
        var set = BuiltInRegistries.BLOCK.Get(tag);
        if (set is null || !set.IsBound) return false;
        return set.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner));
    }

    //InSet 该状态是否属于某个已解析的方块集合
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

    //FindProperty 按属性名在该状态的属性表里查找
    public static PropertyBase? FindProperty(BlockState state, string name)
    {
        foreach (var property in state.GetProperties())
            if (property.Name == name) return property;
        return null;
    }

    //CopyProperties 把源状态的属性逐个搬到目标状态 属性名与取值都合法才写
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

    //ParseStateString 解析 "minecraft:stone[facing=north]" 形态的方块状态串 非法返回 null
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

//StructureHeightmapTypeCodec 高度图类型编解码 对应原版 Heightmap.Types.CODEC
internal sealed class StructureHeightmapTypeCodec : ScalarCodec<NetCraft.Registry.Heightmap.Types>
{
    public static readonly StructureHeightmapTypeCodec Instance = new();

    public override DataResult<NetCraft.Registry.Heightmap.Types> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<NetCraft.Registry.Heightmap.Types>.Error(() => "heightmap 必须是字符串");
        var parsed = NetCraft.Registry.Heightmap.FromSerializationKey(text.GetOrThrow().ToUpperInvariant());
        return parsed is null
            ? DataResult<NetCraft.Registry.Heightmap.Types>.Error(() => $"未知的高度图类型: {text.GetOrThrow()}")
            : DataResult<NetCraft.Registry.Heightmap.Types>.Success(parsed.Value);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.Registry.Heightmap.Types value)
        => DataResult<U>.Success(ops.CreateString(value.GetSerializationKey().ToLowerInvariant()));
}

//StructureCompoundTagCodec 复合标签编解码 对应原版 CompoundTag.CODEC
//原生 Tag 直接透传 JSON 形态的 map 按值逐层转标签 编码只支持原生标签
internal sealed class StructureCompoundTagCodec : ScalarCodec<CompoundTag>
{
    public static readonly StructureCompoundTagCodec Instance = new();

    public override DataResult<CompoundTag> Parse<U>(DynamicOps<U> ops, U input)
        => DataResult<CompoundTag>.Success(ProcessorNbtConversion.ToCompoundTag(ops, input));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, CompoundTag value)
        => value is U native ? DataResult<U>.Success(native) : DataResult<U>.Error(() => "复合标签编码仅支持原生标签");
}

//ProcessorNbtConversion JSON 形态的复合标签转 CompoundTag 对应原版 CompoundTag.CODEC 的跨 ops 转换
internal static class ProcessorNbtConversion
{
    //ToCompoundTag 把一段 map 形态的值转成 CompoundTag 已是原生标签时直接返回
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

    //ToTag 按 JSON 值形态逐层转标签 数字统一按整数与浮点分开
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
