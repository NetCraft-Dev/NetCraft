using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//TypedEntityData 带类型标识的实体存档数据 对应原版 net.minecraft.world.item.component.TypedEntityData
//type 指明数据属于哪种注册项 tag 是去掉 id 之后的实体 NBT
public sealed class TypedEntityData<T> where T : class
{
    internal const string TypeTagName = "id";
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

    //CodecOf 持久化编解码 对应原版 TypedEntityData.codec
    public static Codec<TypedEntityData<T>> CodecOf(Codec<T> typeCodec) => new TypedEntityDataCodec<T>(typeCodec);

    //StripId 去掉 id 键 原版约定 id 由类型字段承载不留在数据里
    private static CompoundTag StripId(CompoundTag tag)
    {
        if (!tag.Contains(TypeTagName)) return tag;
        var copy = (CompoundTag)tag.Copy();
        copy.Remove(TypeTagName);
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

//TypedEntityDataCodec 持久化编解码 对应原版 TypedEntityData.codec
//整块走 CompoundTag.Codec 取出 id 键解析类型 其余字段留作实体数据
internal sealed class TypedEntityDataCodec<T> : ScalarCodec<TypedEntityData<T>> where T : class
{
    private readonly Codec<T> _typeCodec;

    public TypedEntityDataCodec(Codec<T> typeCodec) => _typeCodec = typeCodec;

    public override DataResult<TypedEntityData<T>> Parse<U>(DynamicOps<U> ops, U input)
    {
        return CompoundTag.Codec.Parse(ops, input).FlatMap(tag =>
        {
            var typeTag = tag[TypedEntityData<T>.TypeTagName];
            if (typeTag is null) return DataResult<TypedEntityData<T>>.Error(() => "Expected 'id' field in entity data");
            var copy = (CompoundTag)tag.Copy();
            copy.Remove(TypedEntityData<T>.TypeTagName);
            return _typeCodec.Parse(NbtOps.Instance, typeTag).Map(type => TypedEntityData<T>.Of(type, copy));
        });
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, TypedEntityData<T> value)
    {
        var typeResult = _typeCodec.EncodeStart(NbtOps.Instance, value.Type);
        if (!typeResult.Result().IsPresent) return DataResult<U>.Error(() => "Unable to encode entity data type");
        var tag = value.CopyTagWithoutId();
        tag.Put(TypedEntityData<T>.TypeTagName, typeResult.GetOrThrow());
        return CompoundTag.Codec.EncodeStart(ops, tag);
    }
}
