namespace NetCraft.Network.Protocol;

//CodecModifier codec modifier, maps to vanilla net.minecraft.network.protocol.CodecModifier
//Converts the raw StreamCodec into the final StreamCodec using context C
//Typical use is injecting keys or registries from the configuration context
public interface CodecModifier<B, V, C>
{
    //Apply decorates the raw codec with context C and returns the final codec
    StreamCodec<B, V> Apply(StreamCodec<B, V> original, C context);
}
