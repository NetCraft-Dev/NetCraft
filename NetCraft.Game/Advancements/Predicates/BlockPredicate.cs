using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.Advancements.Predicates;

//BlockPredicate 方块谓词 判定方块类型 状态属性 方块实体数据
//对应原版 net.minecraft.advancements.predicates.BlockPredicate
public sealed record BlockPredicate(
    Optional<HolderSet<NetCraft.Registry.Block>> Blocks,
    Optional<StatePropertiesPredicate> Properties,
    Optional<NbtPredicate> Nbt,
    DataComponentMatchers Components)
{
    //Codec 持久化编解码 字段名 blocks 与 state 与 nbt 与 components 对应原版 CODEC
    public static readonly Codec<BlockPredicate> Codec = RecordCodecBuilder.Of4(
        HolderSetCodecs.BlockSet.OptionalFieldOf("blocks")
            .ForGetter((BlockPredicate predicate) => predicate.Blocks),
        StatePropertiesPredicate.Codec.OptionalFieldOf("state")
            .ForGetter((BlockPredicate predicate) => predicate.Properties),
        NbtPredicate.Codec.OptionalFieldOf("nbt")
            .ForGetter((BlockPredicate predicate) => predicate.Nbt),
        DataComponentMatchers.Codec.FieldOf("components")
            .ForGetter((BlockPredicate predicate) => predicate.Components),
        (blocks, properties, nbt, components) => new BlockPredicate(blocks, properties, nbt, components));

    //RequiresNbt 该谓词是否要求读方块实体数据 对应原版 requiresNbt
    public bool RequiresNbt => Nbt.IsPresent;

    //MatchesState 方块集合与状态属性都要命中 对应原版 matchesState
    public bool MatchesState(BlockState state)
    {
        if (Blocks.IsPresent && !Blocks.Get().Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner)))
            return false;
        if (Properties.IsPresent && !Properties.Get().Matches(state)) return false;
        return true;
    }

    //Matches 位置已加载且状态命中 对应原版 matches
    //nbt 与组件匹配要读方块实体 方块实体体系未接通 有这类要求时按无法验证处理
    public bool Matches(ServerLevel level, BlockPos pos)
    {
        if (!level.IsLoaded(pos)) return false;
        var state = level.GetBlockState(pos);
        if (state is null || !MatchesState(state.Value)) return false;
        return !Nbt.IsPresent && Components.IsEmpty;
    }
}
