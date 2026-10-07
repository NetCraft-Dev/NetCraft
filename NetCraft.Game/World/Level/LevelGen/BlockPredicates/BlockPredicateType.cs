using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//BlockPredicateType block predicate type base, maps to vanilla BlockPredicateType<P>
//Vanilla parameterizes the type by its predicate; NetCraft generics are not covariant, so it is split into a non-generic base plus a generic middle layer
public abstract class BlockPredicateType : NetCraft.Registry.BlockPredicateType
{
    public Identifier Id { get; }

    protected BlockPredicateType(Identifier id) => Id = id;

    //Decode decode a predicate instance from the map; the type field is already consumed by the caller
    public abstract DataResult<BlockPredicate> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //EncodeFields accumulate the instance fields into the builder; the type field is added by the caller
    public abstract void EncodeFields<U>(DynamicOps<U> ops, BlockPredicate value, RecordBuilder<U> builder);

    //Built-in type singletons, one per registry element
    public static readonly BlockPredicateType<MatchingBlocksPredicate> MatchingBlocks =
        new SimpleBlockPredicateType<MatchingBlocksPredicate>("matching_blocks", MatchingBlocksPredicate.Codec);

    public static readonly BlockPredicateType<MatchingBlockTagPredicate> MatchingBlockTag =
        new SimpleBlockPredicateType<MatchingBlockTagPredicate>("matching_block_tag", MatchingBlockTagPredicate.Codec);

    public static readonly BlockPredicateType<MatchingFluidsPredicate> MatchingFluids =
        new SimpleBlockPredicateType<MatchingFluidsPredicate>("matching_fluids", MatchingFluidsPredicate.Codec);

    public static readonly BlockPredicateType<MatchingBiomesPredicate> MatchingBiomes =
        new SimpleBlockPredicateType<MatchingBiomesPredicate>("matching_biomes", MatchingBiomesPredicate.Codec);

    public static readonly BlockPredicateType<HasSturdyFacePredicate> HasSturdyFace =
        new SimpleBlockPredicateType<HasSturdyFacePredicate>("has_sturdy_face", HasSturdyFacePredicate.Codec);

    public static readonly BlockPredicateType<SolidPredicate> Solid =
        new SimpleBlockPredicateType<SolidPredicate>("solid", SolidPredicate.Codec);

    public static readonly BlockPredicateType<ReplaceablePredicate> Replaceable =
        new SimpleBlockPredicateType<ReplaceablePredicate>("replaceable", ReplaceablePredicate.Codec);

    public static readonly BlockPredicateType<WouldSurvivePredicate> WouldSurvive =
        new SimpleBlockPredicateType<WouldSurvivePredicate>("would_survive", WouldSurvivePredicate.Codec);

    public static readonly BlockPredicateType<InsideWorldBoundsPredicate> InsideWorldBounds =
        new SimpleBlockPredicateType<InsideWorldBoundsPredicate>("inside_world_bounds", InsideWorldBoundsPredicate.Codec);

    public static readonly BlockPredicateType<AnyOfPredicate> AnyOf =
        new SimpleBlockPredicateType<AnyOfPredicate>("any_of", AnyOfPredicate.Codec);

    public static readonly BlockPredicateType<AllOfPredicate> AllOf =
        new SimpleBlockPredicateType<AllOfPredicate>("all_of", AllOfPredicate.Codec);

    public static readonly BlockPredicateType<NotPredicate> Not =
        new SimpleBlockPredicateType<NotPredicate>("not", NotPredicate.Codec);

    public static readonly BlockPredicateType<TrueBlockPredicate> True =
        new SimpleBlockPredicateType<TrueBlockPredicate>("true", TrueBlockPredicate.Codec);

    public static readonly BlockPredicateType<UnobstructedPredicate> Unobstructed =
        new SimpleBlockPredicateType<UnobstructedPredicate>("unobstructed", UnobstructedPredicate.Codec);

    //RegisterAll register all built-in types into the BLOCK_PREDICATE_TYPE registry
    public static void RegisterAll()
    {
        Register(MatchingBlocks);
        Register(MatchingBlockTag);
        Register(MatchingFluids);
        Register(MatchingBiomes);
        Register(HasSturdyFace);
        Register(Solid);
        Register(Replaceable);
        Register(WouldSurvive);
        Register(InsideWorldBounds);
        Register(AnyOf);
        Register(AllOf);
        Register(Not);
        Register(True);
        Register(Unobstructed);
    }

    private static void Register(BlockPredicateType type)
        => Registry<NetCraft.Registry.BlockPredicateType>.Register(
            BuiltInRegistries.BLOCK_PREDICATE_TYPE, type.Id, type);
}

//BlockPredicateType<P> generic middle layer for a concrete predicate type; subclasses only supply one MapCodec<P>
public abstract class BlockPredicateType<P> : BlockPredicateType where P : BlockPredicate
{
    private readonly MapCodec<P> _codec;

    protected BlockPredicateType(Identifier id, MapCodec<P> codec) : base(id) => _codec = codec;

    public sealed override DataResult<BlockPredicate> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(value => (BlockPredicate)value);

    public sealed override void EncodeFields<U>(DynamicOps<U> ops, BlockPredicate value, RecordBuilder<U> builder)
    {
        if (value is P typed) _codec.EncodeTo(ops, typed, builder);
    }
}

//SimpleBlockPredicateType type instance carrying only an id and a codec, covering all built-in predicates
internal sealed class SimpleBlockPredicateType<P> : BlockPredicateType<P> where P : BlockPredicate
{
    public SimpleBlockPredicateType(string id, MapCodec<P> codec)
        : base(Identifier.WithDefaultNamespace(id), codec) { }
}
