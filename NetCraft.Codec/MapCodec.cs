namespace NetCraft.Codec;

//Map field serialization interface, mirroring vanilla com.mojang.serialization.MapCodec
//Used for field-level encoding and decoding of record-like structures
public interface MapCodec<T>
{
    //Decode from a MapLike
    DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //Encode into an element under ops, usually a CompoundTag or a similar map structure
    DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value);

    //Accumulate field values into the builder, mirroring vanilla MapCodec.encode
    RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder);

    //Get the record builder used to build field by field
    RecordBuilder<U> Encoder<U>(DynamicOps<U> ops);
}
