using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Items.Component;
using NetCraft.Nbt;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//NbtPredicate NBT 谓词 判定自定义数据组件是否包含期望标签 对应原版 net.minecraft.advancements.predicates.NbtPredicate
public sealed record NbtPredicate(CompoundTag Value) : DataComponentPredicate
{
    //Codec 持久化编解码 宽松解析 SNBT 或结构化标签 对应原版 CODEC
    public static readonly Codec<NbtPredicate> Codec = TagParser<Tag>.LenientCodec.ComapFlatMap(
        tag => DataResult<NbtPredicate>.Success(new NbtPredicate(tag)),
        predicate => predicate.Value);

    //Matches 目标的 custom_data 组件是否包含期望标签
    public bool Matches(DataComponentGetter components)
        => components.Get(DataComponents.CUSTOM_DATA) is CustomData data && data.MatchedBy(Value);

    //Matches 裸标签是否包含期望标签 对应原版接受 Tag 的重载
    public bool Matches(Tag tag) => NbtUtils.CompareNbt(Value, tag, true);
}
