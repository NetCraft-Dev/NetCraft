using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Util.Random;
using RegistryAliasBinding = NetCraft.Registry.PoolAliasBinding;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//PoolAliasBinding 池别名绑定 对应原版 net.minecraft.world.level.levelgen.structure.pools.alias.PoolAliasBinding
//把一个池名改写成另一个池名 生成时先按种子与生成点解出别名映射再查池
//record 才能被三个具体别名继承 原版这里是接口
public abstract record PoolAliasBinding : RegistryAliasBinding
{
    //Codec 别名多态 codec 按 type 字段派发 对应原版 PoolAliasBinding.CODEC
    public static readonly Codec<PoolAliasBinding> Codec = new PoolAliasDispatchCodec();

    //ForEachResolved 把本绑定解成一组别名到目标池的映射 对应原版 forEachResolved
    public abstract void ForEachResolved(RandomSource random, Action<Identifier, Identifier> consumer);

    //AllTargets 本绑定可能指到的全部目标池 对应原版 allTargets 供注册目标池占位
    public abstract IEnumerable<Identifier> AllTargets();
}

//DirectPoolAlias 直接别名 对应原版 DirectPoolAlias
//别名恒指向同一个目标池 不参与随机
public sealed record DirectPoolAlias(Identifier Alias, Identifier Target) : PoolAliasBinding
{
    public static new readonly Codec<DirectPoolAlias> Codec =
        RecordCodecBuilder.Of2<DirectPoolAlias, Identifier, Identifier>(
            StructurePoolCodecs.TemplateLocation.FieldOf("alias").ForGetter<DirectPoolAlias, Identifier>(a => a.Alias),
            StructurePoolCodecs.TemplateLocation.FieldOf("target").ForGetter<DirectPoolAlias, Identifier>(a => a.Target),
            (alias, target) => new DirectPoolAlias(alias, target));

    public override void ForEachResolved(RandomSource random, Action<Identifier, Identifier> consumer)
        => consumer(Alias, Target);

    public override IEnumerable<Identifier> AllTargets() => new[] { Target };

    public override string ToString() => $"DirectPoolAlias[{Alias}->{Target}]";
}

//RandomPoolAlias 随机别名 对应原版 RandomPoolAlias
//别名按权重表随机指到一个目标池 同一生成点解出的映射是固定的
public sealed record RandomPoolAlias(Identifier Alias, WeightedList<Identifier> Targets) : PoolAliasBinding
{
    public static new readonly Codec<RandomPoolAlias> Codec =
        RecordCodecBuilder.Of2<RandomPoolAlias, Identifier, WeightedList<Identifier>>(
            StructurePoolCodecs.TemplateLocation.FieldOf("alias").ForGetter<RandomPoolAlias, Identifier>(a => a.Alias),
            PoolAliasCodecs.WeightedIdentifierList.FieldOf("targets")
                .ForGetter<RandomPoolAlias, WeightedList<Identifier>>(a => a.Targets),
            (alias, targets) => new RandomPoolAlias(alias, targets));

    public override void ForEachResolved(RandomSource random, Action<Identifier, Identifier> consumer)
        => consumer(Alias, Targets.GetRandomOrThrow(random));

    public override IEnumerable<Identifier> AllTargets()
        => Targets.Unwrap().Select(entry => entry.Value);

    public override string ToString() => $"RandomPoolAlias[{Alias}->{Targets.Unwrap().Count} 个目标]";
}

//RandomGroupPoolAlias 随机组别名 对应原版 RandomGroupPoolAlias
//按权重挑一组别名整组生效 组内几条绑定共用同一个随机源 对应原版先挑组再逐条解析
public sealed record RandomGroupPoolAlias(WeightedList<List<PoolAliasBinding>> Groups) : PoolAliasBinding
{
    public static new readonly Codec<RandomGroupPoolAlias> Codec =
        SingleFieldRecordCodec.Of<RandomGroupPoolAlias, WeightedList<List<PoolAliasBinding>>>(
            PoolAliasCodecs.WeightedAliasGroups.FieldOf("groups")
                .ForGetter<RandomGroupPoolAlias, WeightedList<List<PoolAliasBinding>>>(a => a.Groups),
            groups => new RandomGroupPoolAlias(groups));

    public override void ForEachResolved(RandomSource random, Action<Identifier, Identifier> consumer)
    {
        foreach (var binding in Groups.GetRandomOrThrow(random))
            binding.ForEachResolved(random, consumer);
    }

    public override IEnumerable<Identifier> AllTargets()
        => Groups.Unwrap().SelectMany(entry => entry.Value).SelectMany(binding => binding.AllTargets());

    public override string ToString() => $"RandomGroupPoolAlias[{Groups.Unwrap().Count} 组]";
}

//PoolAliasBindings 别名的类型登记 对应原版 PoolAliasBindings.bootstrap
public static class PoolAliasBindings
{
    //RegisterAll 把三种别名的 codec 登记进 POOL_ALIAS_BINDING_TYPE 幂等
    public static void RegisterAll()
    {
        Register("direct", DirectPoolAlias.Codec);
        Register("random", RandomPoolAlias.Codec);
        Register("random_group", RandomGroupPoolAlias.Codec);
    }

    //Register 按短名登记一个别名 codec 具体类型要包一层适配注册表要求的接口形态
    private static void Register<T>(string path, MapCodec<T> codec) where T : PoolAliasBinding
    {
        var id = Identifier.WithDefaultNamespace(path);
        if (BuiltInRegistries.POOL_ALIAS_BINDING_TYPE.ContainsKey(id)) return;
        Registry<MapCodec<RegistryAliasBinding>>.Register(BuiltInRegistries.POOL_ALIAS_BINDING_TYPE, id,
            new PoolAliasBindingMapCodec<T>(codec));
    }
}

//PoolAliasBindingMapCodec 把具体别名的 map codec 适配成注册表持有的接口形态
//泛型 Decode 无法直接协变 只能包一层做类型转换
internal sealed class PoolAliasBindingMapCodec<T> : MapCodec<RegistryAliasBinding> where T : PoolAliasBinding
{
    private readonly MapCodec<T> _inner;

    public PoolAliasBindingMapCodec(MapCodec<T> inner) => _inner = inner;

    public DataResult<RegistryAliasBinding> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _inner.Decode<U>(ops, input).Map(binding => (RegistryAliasBinding)binding!);

    public DataResult<U> EncodeStart<U>(DynamicOps<U> ops, RegistryAliasBinding value)
        => value is T binding
            ? _inner.EncodeStart<U>(ops, binding)
            : DataResult<U>.Error(() => "别名类型与本 codec 不匹配");

    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, RegistryAliasBinding value, RecordBuilder<U> builder)
        => value is T binding ? _inner.EncodeTo<U>(ops, binding, builder) : builder;

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops) => ops.MapBuilder();
}

//SingleFieldRecordCodec 单字段 record codec 原版 RecordCodecBuilder 至少两字段 只有一个字段的 record 走它
internal static class SingleFieldRecordCodec
{
    public static Codec<T> Of<T, F>(FieldCodec<T, F> field, Func<F, T> ctor)
        => new SingleFieldRecordCodecImpl<T, F>(field, ctor);
}

internal sealed class SingleFieldRecordCodecImpl<T, F> : AbstractMapCodec<T>
{
    private readonly FieldCodec<T, F> _field;
    private readonly Func<F, T> _ctor;

    public SingleFieldRecordCodecImpl(FieldCodec<T, F> field, Func<F, T> ctor)
    {
        _field = field;
        _ctor = ctor;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _field.Codec.Decode<U>(ops, input).Map(value => _ctor(value!));

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        _field.Codec.EncodeTo<U>(ops, _field.Getter(value), builder);
        return builder;
    }
}

//PoolAliasCodecs 别名子系统公用 codec 片段
internal static class PoolAliasCodecs
{
    //WeightedIdentifierList 非空权重标识符列表 对应原版 WeightedList.nonEmptyCodec
    public static readonly Codec<WeightedList<Identifier>> WeightedIdentifierList =
        new WeightedListCodec<Identifier>(StructurePoolCodecs.TemplateLocation, true);

    //WeightedAliasGroups 非空权重别名组列表 对应原版 WeightedList.nonEmptyCodec(Codec.list(PoolAliasBinding.CODEC))
    public static readonly Codec<WeightedList<List<PoolAliasBinding>>> WeightedAliasGroups =
        new WeightedListCodec<List<PoolAliasBinding>>(new PoolAliasListCodec(), true);
}

//PoolAliasListCodec 别名列表 codec 逐条解析失败即整体失败 不抛异常
internal sealed class PoolAliasListCodec : ScalarCodec<List<PoolAliasBinding>>
{
    public override DataResult<List<PoolAliasBinding>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent) return DataResult<List<PoolAliasBinding>>.Error(() => "别名组必须是数组");
        var result = new List<PoolAliasBinding>();
        foreach (var element in stream.GetOrThrow())
        {
            var parsed = PoolAliasBinding.Codec.Parse(ops, element);
            if (!parsed.Result().IsPresent)
                return DataResult<List<PoolAliasBinding>>.Error(() => "别名组里的别名解析失败");
            result.Add(parsed.GetOrThrow());
        }
        return DataResult<List<PoolAliasBinding>>.Success(result);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, List<PoolAliasBinding> value)
    {
        var encoded = new List<U>();
        foreach (var binding in value)
        {
            var one = PoolAliasBinding.Codec.EncodeStart(ops, binding);
            if (!one.Result().IsPresent) return DataResult<U>.Error(() => "别名编码失败");
            encoded.Add(one.GetOrThrow());
        }
        return DataResult<U>.Success(ops.CreateList(encoded));
    }
}

//WeightedListCodec 权重列表编解码 元素可以是裸值也可以是带 weight 的 data 对象
internal sealed class WeightedListCodec<E> : ScalarCodec<WeightedList<E>>
{
    private readonly Codec<E> _elementCodec;
    private readonly bool _nonEmpty;

    public WeightedListCodec(Codec<E> elementCodec, bool nonEmpty)
    {
        _elementCodec = elementCodec;
        _nonEmpty = nonEmpty;
    }

    public override DataResult<WeightedList<E>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent) return DataResult<WeightedList<E>>.Error(() => "权重列表必须是数组");

        var entries = new List<Weighted<E>>();
        foreach (var element in stream.GetOrThrow())
        {
            var entry = ReadEntry(ops, element);
            if (!entry.Result().IsPresent) return DataResult<WeightedList<E>>.Error(() => "权重条目的 data 解析失败");
            entries.Add(entry.GetOrThrow());
        }
        if (_nonEmpty && entries.Count == 0)
            return DataResult<WeightedList<E>>.Error(() => "权重列表至少要有一个元素");
        return DataResult<WeightedList<E>>.Success(WeightedList<E>.Of(entries));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, WeightedList<E> value)
    {
        var encoded = new List<U>();
        foreach (var entry in value.Unwrap())
        {
            var data = _elementCodec.EncodeStart(ops, entry.Value);
            if (!data.Result().IsPresent) return DataResult<U>.Error(() => "权重条目的 data 编码失败");
            var builder = ops.MapBuilder();
            builder.Add("data", data.GetOrThrow());
            builder.Add("weight", ops.CreateInt(entry.Weight));
            var built = builder.Build(ops.Empty());
            if (!built.Result().IsPresent) return DataResult<U>.Error(() => "权重条目编码失败");
            encoded.Add(built.GetOrThrow());
        }
        return DataResult<U>.Success(ops.CreateList(encoded));
    }

    //ReadEntry 读一条权重条目 带 data 字段的按对象读 否则按裸值读 对应原版 either 的两侧
    private DataResult<Weighted<E>> ReadEntry<U>(DynamicOps<U> ops, U element)
    {
        var mapResult = ops.GetMap(element);
        if (mapResult.Result().IsPresent && mapResult.GetOrThrow().Get("data").IsPresent)
        {
            var map = mapResult.GetOrThrow();
            var weightTag = map.Get("weight");
            var weight = 1;
            if (weightTag.IsPresent)
            {
                var number = ops.GetNumberValue(weightTag.Get());
                if (!number.Result().IsPresent) return DataResult<Weighted<E>>.Error(() => "weight 必须是数字");
                weight = (int)number.GetOrThrow();
                if (weight <= 0) return DataResult<Weighted<E>>.Error(() => $"weight 必须为正 实际 {weight}");
            }
            var dataTag = map.Get("data");
            return _elementCodec.Parse(ops, dataTag.Get()).Map(e => new Weighted<E>(e, weight));
        }
        return _elementCodec.Parse(ops, element).Map(e => new Weighted<E>(e, 1));
    }
}

//PoolAliasDispatchCodec 按 type 字段查 POOL_ALIAS_BINDING_TYPE 再派发 对应原版 dispatch codec
internal sealed class PoolAliasDispatchCodec : ScalarCodec<PoolAliasBinding>
{
    public override DataResult<PoolAliasBinding> Parse<U>(DynamicOps<U> ops, U input)
    {
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return DataResult<PoolAliasBinding>.Error(() => "池别名必须是对象");
        var map = mapResult.GetOrThrow();

        var typeTag = map.Get("type");
        if (!typeTag.IsPresent) return DataResult<PoolAliasBinding>.Error(() => "池别名缺少 type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent) return DataResult<PoolAliasBinding>.Error(() => "池别名 type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null) return DataResult<PoolAliasBinding>.Error(() => $"非法的池别名类型: {typeText.GetOrThrow()}");

        var codec = BuiltInRegistries.POOL_ALIAS_BINDING_TYPE.GetValue(typeId.Value);
        if (codec is null) return DataResult<PoolAliasBinding>.Error(() => $"未注册的池别名类型: {typeId}");

        return codec.Decode<U>(ops, map).FlatMap(binding => binding is PoolAliasBinding gameBinding
            ? DataResult<PoolAliasBinding>.Success(gameBinding)
            : DataResult<PoolAliasBinding>.Error(() => $"池别名 {typeId} 不是 Game 层实现"));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, PoolAliasBinding value)
        => value switch
        {
            DirectPoolAlias direct => EncodeTyped(ops, "direct", DirectPoolAlias.Codec, direct),
            RandomPoolAlias random => EncodeTyped(ops, "random", RandomPoolAlias.Codec, random),
            RandomGroupPoolAlias group => EncodeTyped(ops, "random_group", RandomGroupPoolAlias.Codec, group),
            _ => DataResult<U>.Error(() => $"不支持的池别名 {value.GetType().Name}"),
        };

    //EncodeTyped 按具体类型编码并补上 type 字段
    private static DataResult<U> EncodeTyped<U, T>(DynamicOps<U> ops, string type, MapCodec<T> codec, T value)
        where T : PoolAliasBinding
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace(type).ToString()));
        codec.EncodeTo<U>(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}
