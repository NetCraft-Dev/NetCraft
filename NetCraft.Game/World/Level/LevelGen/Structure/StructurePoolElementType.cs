using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePoolElementType pool element type base class, maps to vanilla StructurePoolElementType
//Holds the registry name and the decode entry point; loading dispatches to a concrete type by the element's element_type field
public abstract class StructurePoolElementType : NetCraft.Registry.StructurePoolElementType<object>
{
    public Identifier Id { get; }

    protected StructurePoolElementType(Identifier id) => Id = id;

    //Decode decodes a pool element from a map; the element_type field is already consumed by the caller
    public abstract DataResult<StructurePoolElement> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //Encode writes out a pool element; the concrete type's map codec encodes it and then the element_type field is added
    public abstract DataResult<U> Encode<U>(DynamicOps<U> ops, StructurePoolElement element);

    //WithTypeField adds the element_type field to an already encoded pool element map
    //Vanilla adds the field automatically via the dispatch codec; our codec framework lacks that, so we merge a key after encoding
    protected DataResult<U> WithTypeField<U>(DynamicOps<U> ops, DataResult<U> encoded)
        => encoded.Result().IsPresent
            ? ops.MergeToMap(encoded.GetOrThrow(), ops.CreateString("element_type"), ops.CreateString(Id.ToString()))
            : encoded;

    //Register registers into STRUCTURE_POOL_ELEMENT and returns itself so a static field can be assigned directly
    protected static T Register<T>(Identifier id, T type) where T : StructurePoolElementType
    {
        Registry<NetCraft.Registry.StructurePoolElementType<object>>.Register(
            BuiltInRegistries.STRUCTURE_POOL_ELEMENT, id, type);
        return type;
    }

    public override string ToString() => $"StructurePoolElementType[{Id}]";
}

//SinglePoolElementType single-template element type, maps to vanilla StructurePoolElementType.SINGLE
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

//LegacySinglePoolElementType legacy single-template element type, maps to vanilla StructurePoolElementType.LEGACY
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

//ListPoolElementType template list element type, maps to vanilla StructurePoolElementType.LIST
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

//FeaturePoolElementType feature element type, maps to vanilla StructurePoolElementType.FEATURE
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

//EmptyPoolElementType empty element type, maps to vanilla StructurePoolElementType.EMPTY
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
