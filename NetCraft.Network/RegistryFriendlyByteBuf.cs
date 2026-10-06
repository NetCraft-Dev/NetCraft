using NetCraft.Registry;

namespace NetCraft.Network;

//RegistryFriendlyByteBuf protocol buffer with RegistryAccess, maps to vanilla net.minecraft.network.RegistryFriendlyByteBuf
//Inherits FriendlyByteBuf and additionally holds RegistryAccess so StreamCodec can encode and decode by registry id
public sealed class RegistryFriendlyByteBuf : FriendlyByteBuf
{
    //RegistryAccess is the registry access entry point, looking up a Registry by ResourceKey
    //Writable so the outbound encode buffer can be reused across phases: the Login phase has no registry, it is only available after entering Play
    public RegistryAccess RegistryAccess { get; set; }

    public RegistryFriendlyByteBuf(RegistryAccess registryAccess) : base()
    {
        RegistryAccess = registryAccess;
    }

    public RegistryFriendlyByteBuf(RegistryAccess registryAccess, byte[] data) : base(data)
    {
        RegistryAccess = registryAccess;
    }

    //Lookup looks up a Registry by registry key, throwing when not found
    public Registry<E> Lookup<E>(ResourceKey<Registry<E>> registryKey) where E : class
        => RegistryAccess.LookupOrThrow(registryKey);
}
