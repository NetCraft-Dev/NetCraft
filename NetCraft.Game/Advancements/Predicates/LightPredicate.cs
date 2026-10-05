using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Advancements.Predicates;

//LightPredicate 光照谓词 判定位置的最大局部亮度落在区间
//对应原版 net.minecraft.advancements.predicates.LightPredicate
public sealed record LightPredicate(MinMaxBounds.Ints Composite)
{
    //Codec 持久化编解码 字段名 light 对应原版 CODEC
    public static readonly Codec<LightPredicate> Codec = RecordCodecBuilder.Of1(
        MinMaxBounds.Ints.CODEC.OptionalFieldOf("light", MinMaxBounds.Ints.Any)
            .ForGetter((LightPredicate predicate) => predicate.Composite),
        composite => new LightPredicate(composite));

    //Matches 位置已加载且亮度落在区间 对应原版 matches
    public bool Matches(ServerLevel level, BlockPos pos)
        => level.IsLoaded(pos) && Composite.Matches(level.GetMaxLocalRawBrightness(pos));
}
