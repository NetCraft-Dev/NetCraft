using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component;

//CustomData 自由格式的自定义数据组件 对应原版 net.minecraft.world.item.component.CustomData
//承载一块不透明的 NBT 交给外部改之前一律先复制标签
public sealed record CustomData(CompoundTag Tag)
{
    public static readonly CustomData Empty = new(new CompoundTag());

    //CompoundTagCodec 复合标签持久化编解码 对应原版 COMPOUND_TAG_CODEC
    //先按结构化标签解 失败再按 SNBT 字符串解
    public static readonly Codec<CompoundTag> CompoundTagCodec = Codecs.WithAlternative(CompoundTag.Codec, TagParser<Tag>.FlattenedCodec);

    //PersistentCodec 对应原版 CODEC
    public static readonly Codec<CustomData> PersistentCodec = CompoundTagCodec.ComapFlatMap(
        tag => DataResult<CustomData>.Success(new CustomData(tag)),
        data => data.Tag);

    //StreamCodec 对应原版 STREAM_CODEC 直接写整块复合标签
    public static readonly StreamCodec<RegistryFriendlyByteBuf, CustomData> StreamCodec = new CustomDataStreamCodec();

    //Of 复制一份标签再构造 原版约定外部标签不复制不能直接持有
    public static CustomData Of(CompoundTag tag) => new((CompoundTag)tag.Copy());

    //MatchedBy 期望标签是否被当前数据包含 对应原版 matchedBy
    public bool MatchedBy(CompoundTag expectedTag) => NbtUtils.CompareNbt(expectedTag, Tag, true);

    //Update 改物品栈上的该组件 改完为空就整个移除 对应原版静态 update
    public static void Update(DataComponentType<CustomData> component, ItemStack itemStack, Action<CompoundTag> consumer)
    {
        var updated = itemStack.GetOrDefault(component, Empty).Update(consumer);
        if (updated.Tag.IsEmpty) itemStack.Remove(component);
        else itemStack.Set(component, updated);
    }

    //Set 直接替换物品栈上的该组件 空标签等价移除 对应原版静态 set
    public static void Set(DataComponentType<CustomData> component, ItemStack itemStack, CompoundTag tag)
    {
        if (!tag.IsEmpty) itemStack.Set(component, Of(tag));
        else itemStack.Remove(component);
    }

    //Update 复制一份再交给回调改 对应原版实例 update
    public CustomData Update(Action<CompoundTag> consumer)
    {
        var copy = (CompoundTag)Tag.Copy();
        consumer(copy);
        return new CustomData(copy);
    }

    public bool IsEmpty => Tag.IsEmpty;

    //CopyTag 取数据副本 调用方拿到的不能是内部引用
    public CompoundTag CopyTag() => (CompoundTag)Tag.Copy();

    public bool Contains(string name) => Tag.Contains(name);

    //判等随 record 走 成员是 CompoundTag 而它已按内容判等 与原版一致
    public override string ToString() => $"CustomData[{Tag}]";
}

//CustomDataStreamCodec 对应原版 STREAM_CODEC 整块复合标签进出
internal sealed class CustomDataStreamCodec : StreamCodec<RegistryFriendlyByteBuf, CustomData>
{
    public CustomData Decode(RegistryFriendlyByteBuf buf) => new((CompoundTag)buf.ReadNbt());

    public void Encode(RegistryFriendlyByteBuf buf, CustomData value) => buf.WriteNbt(value.Tag);
}
