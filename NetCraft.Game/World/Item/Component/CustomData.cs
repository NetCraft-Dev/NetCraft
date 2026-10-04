using NetCraft.Codec;
using NetCraft.Nbt;

namespace NetCraft.Game.World.Items.Component;

//CustomData 自由格式的自定义数据组件 对应原版 net.minecraft.world.item.component.CustomData
//这里只补持久化所需的核心 其余访问器待用到再补
public sealed record CustomData(CompoundTag Tag)
{
    public static readonly CustomData Empty = new(new CompoundTag());

    //CompoundTagCodec 复合标签的持久化编解码 对应原版 COMPOUND_TAG_CODEC
    public static readonly Codec<CompoundTag> CompoundTagCodec = CompoundTag.Codec;

    //PersistentCodec 对应原版 CODEC
    public static readonly Codec<CustomData> PersistentCodec = CompoundTag.Codec.ComapFlatMap(
        tag => DataResult<CustomData>.Success(new CustomData(tag)),
        data => data.Tag);

    //CopyTag 取数据副本 调用方拿到的不能是内部引用
    public CompoundTag CopyTag() => (CompoundTag)Tag.Copy();

    public bool Contains(string name) => Tag.Contains(name);

    public override string ToString() => $"CustomData[{Tag}]";
}
