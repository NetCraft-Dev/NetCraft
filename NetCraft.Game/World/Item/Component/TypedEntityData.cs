using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Registry;

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

    //判等要求类型与数据都相同 对应原版 equals
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj)
            || (obj is TypedEntityData<T> other && Equals(_type, other._type) && _tag.Equals(other._tag));

    //哈希与判等一致 对应原版 hashCode
    public override int GetHashCode() => HashCode.Combine(_type, _tag);

    public override string ToString() => $"{_type} {_tag}";

    public bool Contains(string name) => _tag.Contains(name);

    //CopyTagWithoutId 取不含 id 的数据副本
    public CompoundTag CopyTagWithoutId() => (CompoundTag)_tag.Copy();

    //LoadInto 把这份数据盖到实体上 对应原版 loadInto(Entity)
    //原版走 ProblemReporter 与 TagValueOutput 一整套上下文写入 NC 尚未接入
    //这里直接走实体的 CompoundTag 读写 并保住原 UUID 不被数据里的值顶掉
    public void LoadInto(NetCraft.Registry.Entity entity)
    {
        var entityData = new CompoundTag();
        entity.SaveWithoutId(entityData);
        entityData.Merge(CopyTagWithoutId());
        var uuid = entity.Uuid;
        entity.Load(entityData);
        entity.Uuid = uuid;
    }

    //LoadInto 把这份数据盖到方块实体上 对应原版 loadInto(BlockEntity)
    //只有内容真的变了才回写 失败时用旧标签回滚 返回是否应用成功
    //NC 的方块实体落盘是全量写 没有基类的 setChanged 脏标记 那一步省略
    public bool LoadInto(BlockEntity blockEntity)
    {
        var newTag = new CompoundTag();
        blockEntity.SaveAdditional(newTag);
        var oldTag = (CompoundTag)newTag.Copy();
        newTag.Merge(CopyTagWithoutId());
        if (NbtUtils.AreEqual(newTag, oldTag)) return false;
        try
        {
            blockEntity.LoadCustomOnly(newTag);
            return true;
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to apply custom data to block entity at {blockEntity.Pos}: {e.Message}");
            blockEntity.LoadCustomOnly(oldTag);
            return false;
        }
    }

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
