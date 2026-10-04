using NetCraft.Registry;

namespace NetCraft.Network;

//RegistryFriendlyByteBuf 带 RegistryAccess 的协议缓冲对应原版 net.minecraft.network.RegistryFriendlyByteBuf
//继承 FriendlyByteBuf 额外持有 RegistryAccess 供 StreamCodec 按注册表 id 编解码
public sealed class RegistryFriendlyByteBuf : FriendlyByteBuf
{
    //RegistryAccess 注册表访问入口按 ResourceKey 查 Registry
    //可写是为了让出站编码缓冲能跨阶段复用 Login 阶段无注册表 进 Play 后才有
    public RegistryAccess RegistryAccess { get; set; }

    public RegistryFriendlyByteBuf(RegistryAccess registryAccess) : base()
    {
        RegistryAccess = registryAccess;
    }

    public RegistryFriendlyByteBuf(RegistryAccess registryAccess, byte[] data) : base(data)
    {
        RegistryAccess = registryAccess;
    }

    //Lookup 按注册表 key 查 Registry 找不到抛异常
    public Registry<E> Lookup<E>(ResourceKey<Registry<E>> registryKey) where E : class
        => RegistryAccess.LookupOrThrow(registryKey);
}
