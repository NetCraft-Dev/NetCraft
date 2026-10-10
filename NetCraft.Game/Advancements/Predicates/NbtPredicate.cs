using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Items.Component;
using NetCraft.Nbt;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//NbtPredicate NBT predicate, checks whether the custom data component contains the expected tag, maps to vanilla net.minecraft.advancements.predicates.NbtPredicate
public sealed record NbtPredicate(CompoundTag Value) : DataComponentPredicate
{
    //Codec persistence codec, lenient parsing of SNBT or structured tags, maps to vanilla CODEC
    public static readonly Codec<NbtPredicate> Codec = TagParser<Tag>.LenientCodec.ComapFlatMap(
        tag => DataResult<NbtPredicate>.Success(new NbtPredicate(tag)),
        predicate => predicate.Value);

    //Matches whether the target's custom_data component contains the expected tag
    public bool Matches(DataComponentGetter components)
        => components.Get(DataComponents.CUSTOM_DATA) is CustomData data && data.MatchedBy(Value);

    //Matches whether the bare tag contains the expected tag, maps to the vanilla overload accepting Tag
    public bool Matches(Tag tag) => NbtUtils.CompareNbt(Value, tag, true);
}
