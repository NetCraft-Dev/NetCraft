using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component;

//TypedEntityData entity save data with a type marker, maps to vanilla net.minecraft.world.item.component.TypedEntityData
//type says which registry entry the data belongs to, tag is the entity NBT with the id removed
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

    //Equality requires both type and data to match, maps to vanilla equals
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj)
            || (obj is TypedEntityData<T> other && Equals(_type, other._type) && _tag.Equals(other._tag));

    //Hash matches equality, maps to vanilla hashCode
    public override int GetHashCode() => HashCode.Combine(_type, _tag);

    public override string ToString() => $"{_type} {_tag}";

    public bool Contains(string name) => _tag.Contains(name);

    //CopyTagWithoutId returns a data copy without the id
    public CompoundTag CopyTagWithoutId() => (CompoundTag)_tag.Copy();

    //LoadInto applies this data onto an entity, maps to vanilla loadInto(Entity)
    //Vanilla writes through the full ProblemReporter and TagValueOutput context, not yet wired up in NC
    //Here it goes straight through the entity's CompoundTag read/write and keeps the original UUID from being overwritten by a value in the data
    public void LoadInto(NetCraft.Registry.Entity entity)
    {
        var entityData = new CompoundTag();
        entity.SaveWithoutId(entityData);
        entityData.Merge(CopyTagWithoutId());
        var uuid = entity.Uuid;
        entity.Load(entityData);
        entity.Uuid = uuid;
    }

    //LoadInto applies this data onto a block entity, maps to vanilla loadInto(BlockEntity)
    //Writes back only when the contents actually changed, rolls back to the old tag on failure, returns whether it applied
    //NC block entities save in full with no base-class setChanged dirty flag, so that step is skipped
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

    //StreamCodecOf network codec, writes the type then the whole NBT
    public static StreamCodec<RegistryFriendlyByteBuf, TypedEntityData<T>> StreamCodecOf(StreamCodec<RegistryFriendlyByteBuf, T> typeCodec)
        => new TypedEntityDataStreamCodec<T>(typeCodec);

    //CodecOf persistence codec, maps to vanilla TypedEntityData.codec
    public static Codec<TypedEntityData<T>> CodecOf(Codec<T> typeCodec) => new TypedEntityDataCodec<T>(typeCodec);

    //StripId removes the id key; vanilla requires the id to be carried by the type field, not left in the data
    private static CompoundTag StripId(CompoundTag tag)
    {
        if (!tag.Contains(TypeTagName)) return tag;
        var copy = (CompoundTag)tag.Copy();
        copy.Remove(TypeTagName);
        return copy;
    }
}

//TypedEntityDataStreamCodec maps to the composite of vanilla streamCodec
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

//TypedEntityDataCodec persistence codec, maps to vanilla TypedEntityData.codec
//Goes through CompoundTag.Codec as a whole, pulls the id key to resolve the type and leaves the rest as entity data
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
