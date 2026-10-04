namespace NetCraft.Network.Protocol;

//CodecModifier 编解码器修饰器对应原版 net.minecraft.network.protocol.CodecModifier
//按上下文 C 把原始 StreamCodec 转为最终 StreamCodec
//典型用途按配置上下文注入密钥或注册表
public interface CodecModifier<B, V, C>
{
    //Apply 用上下文 C 修饰原始 codec 返回最终 codec
    StreamCodec<B, V> Apply(StreamCodec<B, V> original, C context);
}
