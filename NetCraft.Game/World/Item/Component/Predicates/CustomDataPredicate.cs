using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//CustomDataPredicate 自定义数据谓词 判定 custom_data 是否包含期望标签
//对应原版 net.minecraft.core.component.predicates.CustomDataPredicate
public sealed record CustomDataPredicate(CompoundTag Value) : SingleComponentItemPredicate<CustomData>
{
    //Codec 持久化编解码 宽松解析 SNBT 或结构化标签 对应原版 CODEC
    public static readonly Codec<CustomDataPredicate> Codec = TagParser<Tag>.LenientCodec.ComapFlatMap(
        tag => DataResult<CustomDataPredicate>.Success(new CustomDataPredicate(tag)),
        predicate => predicate.Value);

    public DataComponentType<object> ComponentType => DataComponents.CUSTOM_DATA;

    public bool MatchesValue(CustomData value) => value.MatchedBy(Value);
}
