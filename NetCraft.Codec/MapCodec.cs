namespace NetCraft.Codec;

//map字段序列化接口对应原版com.mojang.serialization.MapCodec
//用于record-like结构的字段级编码解码
public interface MapCodec<T>
{
    //从MapLike解码
    DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //编码为ops下的元素通常返回CompoundTag或类似map结构
    DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value);

    //把字段值累积到builder对应原版MapCodec.encode
    RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder);

    //获取record builder用于逐字段构建
    RecordBuilder<U> Encoder<U>(DynamicOps<U> ops);
}
