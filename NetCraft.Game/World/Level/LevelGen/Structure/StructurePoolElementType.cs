using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePoolElementType 池元素类型基类 对应原版 StructurePoolElementType
//持注册名与 decode 入口 装载时按元素的 element_type 字段派发到具体类型
public abstract class StructurePoolElementType : NetCraft.Registry.StructurePoolElementType<object>
{
    public Identifier Id { get; }

    protected StructurePoolElementType(Identifier id) => Id = id;

    //Decode 从 map 解出一个池元素 element_type 字段已由外层消费
    public abstract DataResult<StructurePoolElement> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //Encode 写出一个池元素 由具体类型的 map codec 编码后补上 element_type 字段
    public abstract DataResult<U> Encode<U>(DynamicOps<U> ops, StructurePoolElement element);

    //WithTypeField 给已编码的池元素 map 补上 element_type 字段
    //原版靠 dispatch codec 自动补字段 我们的 codec 框架没有对应能力 只能编码后再并一个键
    protected DataResult<U> WithTypeField<U>(DynamicOps<U> ops, DataResult<U> encoded)
        => encoded.Result().IsPresent
            ? ops.MergeToMap(encoded.GetOrThrow(), ops.CreateString("element_type"), ops.CreateString(Id.ToString()))
            : encoded;

    //Register 注册进 STRUCTURE_POOL_ELEMENT 并返回自身 便于静态字段直接赋值
    protected static T Register<T>(Identifier id, T type) where T : StructurePoolElementType
    {
        Registry<NetCraft.Registry.StructurePoolElementType<object>>.Register(
            BuiltInRegistries.STRUCTURE_POOL_ELEMENT, id, type);
        return type;
    }

    public override string ToString() => $"StructurePoolElementType[{Id}]";
}

//SinglePoolElementType 单模板元素类型 对应原版 StructurePoolElementType.SINGLE
public sealed class SinglePoolElementType : StructurePoolElementType
{
    public static readonly SinglePoolElementType Instance =
        Register(Identifier.WithDefaultNamespace("single_pool_element"), new SinglePoolElementType());

    private SinglePoolElementType()
        : base(Identifier.WithDefaultNamespace("single_pool_element")) { }

    public override DataResult<StructurePoolElement> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => SinglePoolElement.MapCodec.Decode(ops, input).Map(element => (StructurePoolElement)element);

    public override DataResult<U> Encode<U>(DynamicOps<U> ops, StructurePoolElement element)
        => WithTypeField(ops, SinglePoolElement.MapCodec.EncodeStart(ops, (SinglePoolElement)element));
}

//LegacySinglePoolElementType 旧版单模板元素类型 对应原版 StructurePoolElementType.LEGACY
public sealed class LegacySinglePoolElementType : StructurePoolElementType
{
    public static readonly LegacySinglePoolElementType Instance =
        Register(Identifier.WithDefaultNamespace("legacy_single_pool_element"), new LegacySinglePoolElementType());

    private LegacySinglePoolElementType()
        : base(Identifier.WithDefaultNamespace("legacy_single_pool_element")) { }

    public override DataResult<StructurePoolElement> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => LegacySinglePoolElement.MapCodec.Decode(ops, input).Map(element => (StructurePoolElement)element);

    public override DataResult<U> Encode<U>(DynamicOps<U> ops, StructurePoolElement element)
        => WithTypeField(ops, LegacySinglePoolElement.MapCodec.EncodeStart(ops, (LegacySinglePoolElement)element));
}

//ListPoolElementType 模板列表元素类型 对应原版 StructurePoolElementType.LIST
public sealed class ListPoolElementType : StructurePoolElementType
{
    public static readonly ListPoolElementType Instance =
        Register(Identifier.WithDefaultNamespace("list_pool_element"), new ListPoolElementType());

    private ListPoolElementType()
        : base(Identifier.WithDefaultNamespace("list_pool_element")) { }

    public override DataResult<StructurePoolElement> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => ListPoolElement.MapCodec.Decode(ops, input).Map(element => (StructurePoolElement)element);

    public override DataResult<U> Encode<U>(DynamicOps<U> ops, StructurePoolElement element)
        => WithTypeField(ops, ListPoolElement.MapCodec.EncodeStart(ops, (ListPoolElement)element));
}

//FeaturePoolElementType 特征元素类型 对应原版 StructurePoolElementType.FEATURE
public sealed class FeaturePoolElementType : StructurePoolElementType
{
    public static readonly FeaturePoolElementType Instance =
        Register(Identifier.WithDefaultNamespace("feature_pool_element"), new FeaturePoolElementType());

    private FeaturePoolElementType()
        : base(Identifier.WithDefaultNamespace("feature_pool_element")) { }

    public override DataResult<StructurePoolElement> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => FeaturePoolElement.MapCodec.Decode(ops, input).Map(element => (StructurePoolElement)element);

    public override DataResult<U> Encode<U>(DynamicOps<U> ops, StructurePoolElement element)
        => WithTypeField(ops, FeaturePoolElement.MapCodec.EncodeStart(ops, (FeaturePoolElement)element));
}

//EmptyPoolElementType 空元素类型 对应原版 StructurePoolElementType.EMPTY
public sealed class EmptyPoolElementType : StructurePoolElementType
{
    public static readonly EmptyPoolElementType Instance =
        Register(Identifier.WithDefaultNamespace("empty_pool_element"), new EmptyPoolElementType());

    private EmptyPoolElementType()
        : base(Identifier.WithDefaultNamespace("empty_pool_element")) { }

    public override DataResult<StructurePoolElement> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => EmptyPoolElement.MapCodec.Decode(ops, input).Map(element => (StructurePoolElement)element);

    public override DataResult<U> Encode<U>(DynamicOps<U> ops, StructurePoolElement element)
        => WithTypeField(ops, EmptyPoolElement.MapCodec.EncodeStart(ops, (EmptyPoolElement)element));
}
