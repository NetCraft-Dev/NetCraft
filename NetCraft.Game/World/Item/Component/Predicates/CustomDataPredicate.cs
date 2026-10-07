using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//CustomDataPredicate custom data predicate, checks whether custom_data contains the expected tag
//Maps to vanilla net.minecraft.core.component.predicates.CustomDataPredicate
public sealed record CustomDataPredicate(CompoundTag Value) : SingleComponentItemPredicate<CustomData>
{
    //Codec persistence codec, leniently decodes SNBT or a structured tag, maps to vanilla CODEC
    public static readonly Codec<CustomDataPredicate> Codec = TagParser<Tag>.LenientCodec.ComapFlatMap(
        tag => DataResult<CustomDataPredicate>.Success(new CustomDataPredicate(tag)),
        predicate => predicate.Value);

    public DataComponentType<object> ComponentType => DataComponents.CUSTOM_DATA;

    public bool MatchesValue(CustomData value) => value.MatchedBy(Value);
}
