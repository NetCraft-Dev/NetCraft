using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//BlockPredicateType 方块谓词类型基类对应原版 BlockPredicateType<P>
//原版类型带谓词泛型 NetCraft 泛型不能协变 拆成非泛型基类加泛型中间层
public abstract class BlockPredicateType : NetCraft.Registry.BlockPredicateType
{
    public Identifier Id { get; }

    protected BlockPredicateType(Identifier id) => Id = id;

    //Decode 从 map 解出一个谓词实例 type 字段已由外层消费
    public abstract DataResult<BlockPredicate> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //EncodeFields 把实例参数累积进 builder type 字段由外层补
    public abstract void EncodeFields<U>(DynamicOps<U> ops, BlockPredicate value, RecordBuilder<U> builder);

    //内置类型单例 与注册表元素一一对应
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

    //RegisterAll 把全部内置类型注册进 BLOCK_PREDICATE_TYPE 注册表
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

//BlockPredicateType<P> 具体谓词类型的泛型中间层 子类只需给出一个 MapCodec<P>
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

//SimpleBlockPredicateType 只带 id 与 codec 的类型实例 覆盖全部内置谓词
internal sealed class SimpleBlockPredicateType<P> : BlockPredicateType<P> where P : BlockPredicate
{
    public SimpleBlockPredicateType(string id, MapCodec<P> codec)
        : base(Identifier.WithDefaultNamespace(id), codec) { }
}
