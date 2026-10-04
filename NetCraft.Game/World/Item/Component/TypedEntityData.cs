using NetCraft.Nbt;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//TypedEntityData 带类型标识的实体存档数据 对应原版 net.minecraft.world.item.component.TypedEntityData
//type 指明数据属于哪种注册项 tag 是去掉 id 之后的实体 NBT
public sealed class TypedEntityData<T> where T : class
{
    private const string TypeTag = "id";
    private readonly T _type;
    private readonly CompoundTag _tag;

    private TypedEntityData(T type, CompoundTag tag)
    {
        _type = type;
        _tag = StripId(tag);
    }

    public static TypedEntityData<T> Of(T type, CompoundTag tag) => new(type, tag);

    public T Type => _type;

    public bool Contains(string name) => _tag.Contains(name);

    //CopyTagWithoutId 取不含 id 的数据副本
    public CompoundTag CopyTagWithoutId() => (CompoundTag)_tag.Copy();

    //StreamCodecOf 网络编解码 先写类型再写整块 NBT
    public static StreamCodec<RegistryFriendlyByteBuf, TypedEntityData<T>> StreamCodecOf(StreamCodec<RegistryFriendlyByteBuf, T> typeCodec)
        => new TypedEntityDataStreamCodec<T>(typeCodec);

    //StripId 去掉 id 键 原版约定 id 由类型字段承载不留在数据里
    private static CompoundTag StripId(CompoundTag tag)
    {
        if (!tag.Contains(TypeTag)) return tag;
        var copy = (CompoundTag)tag.Copy();
        copy.Remove(TypeTag);
        return copy;
    }
}

//TypedEntityDataStreamCodec 对应原版 streamCodec 的 composite 组合
internal sealed class TypedEntityDataStreamCodec<T> : StreamCodec<RegistryFriendlyByteBuf, TypedEntityData<T>> where T : class
{
    private readonly StreamCodec<RegistryFriendlyByteBuf, T> _typeCodec;

    public TypedEntityDataStreamCodec(StreamCodec<RegistryFriendlyByteBuf, T> typeCodec) => _typeCodec = typeCodec;

    public TypedEntityData<T> Decode(RegistryFriendlyByteBuf buf)
    {
        var type = _typeCodec.Decode(buf);
        var tag = buf.ReadNbt();
        return TypedEntityData<T>.Of(type, (CompoundTag)tag);
    }

    public void Encode(RegistryFriendlyByteBuf buf, TypedEntityData<T> value)
    {
        _typeCodec.Encode(buf, value.Type);
        buf.WriteNbt(value.CopyTagWithoutId());
    }
}
