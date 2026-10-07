using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component;

//CustomData free-form custom data component, maps to vanilla net.minecraft.world.item.component.CustomData
//Carries an opaque NBT blob; the tag is always copied before being handed out for modification
public sealed record CustomData(CompoundTag Tag)
{
    public static readonly CustomData Empty = new(new CompoundTag());

    //CompoundTagCodec compound tag persistence codec, maps to vanilla COMPOUND_TAG_CODEC
    //Decodes as a structured tag first, falling back to an SNBT string
    public static readonly Codec<CompoundTag> CompoundTagCodec = Codecs.WithAlternative(CompoundTag.Codec, TagParser<Tag>.FlattenedCodec);

    //PersistentCodec maps to vanilla CODEC
    public static readonly Codec<CustomData> PersistentCodec = CompoundTagCodec.ComapFlatMap(
        tag => DataResult<CustomData>.Success(new CustomData(tag)),
        data => data.Tag);

    //StreamCodec maps to vanilla STREAM_CODEC, writes the whole compound tag directly
    public static readonly StreamCodec<RegistryFriendlyByteBuf, CustomData> StreamCodec = new CustomDataStreamCodec();

    //Of copies the tag before constructing; vanilla requires external tags to be copied before being held
    public static CustomData Of(CompoundTag tag) => new((CompoundTag)tag.Copy());

    //MatchedBy whether the current data contains the expected tag, maps to vanilla matchedBy
    public bool MatchedBy(CompoundTag expectedTag) => NbtUtils.CompareNbt(expectedTag, Tag, true);

    //Update modifies the component on an item stack, removing it entirely when it becomes empty, maps to vanilla static update
    public static void Update(DataComponentType<CustomData> component, ItemStack itemStack, Action<CompoundTag> consumer)
    {
        var updated = itemStack.GetOrDefault(component, Empty).Update(consumer);
        if (updated.Tag.IsEmpty) itemStack.Remove(component);
        else itemStack.Set(component, updated);
    }

    //Set replaces the component on an item stack directly, an empty tag is equivalent to removing, maps to vanilla static set
    public static void Set(DataComponentType<CustomData> component, ItemStack itemStack, CompoundTag tag)
    {
        if (!tag.IsEmpty) itemStack.Set(component, Of(tag));
        else itemStack.Remove(component);
    }

    //Update copies and hands it to a callback to modify, maps to vanilla instance update
    public CustomData Update(Action<CompoundTag> consumer)
    {
        var copy = (CompoundTag)Tag.Copy();
        consumer(copy);
        return new CustomData(copy);
    }

    public bool IsEmpty => Tag.IsEmpty;

    //CopyTag returns a data copy, callers must not receive the internal reference
    public CompoundTag CopyTag() => (CompoundTag)Tag.Copy();

    public bool Contains(string name) => Tag.Contains(name);

    //Equality follows the record, the member is a CompoundTag which already compares by content, same as vanilla
    public override string ToString() => $"CustomData[{Tag}]";
}

//CustomDataStreamCodec maps to vanilla STREAM_CODEC, the whole compound tag goes in and out
internal sealed class CustomDataStreamCodec : StreamCodec<RegistryFriendlyByteBuf, CustomData>
{
    public CustomData Decode(RegistryFriendlyByteBuf buf) => new((CompoundTag)buf.ReadNbt());

    public void Encode(RegistryFriendlyByteBuf buf, CustomData value) => buf.WriteNbt(value.Tag);
}
