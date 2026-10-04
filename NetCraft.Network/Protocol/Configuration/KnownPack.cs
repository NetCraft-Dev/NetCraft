using NetCraft.Config;

namespace NetCraft.Network.Protocol.Configuration;

//KnownPack 已知资源包条目对应原版 net.minecraft.server.packs.repository.KnownPack
//含 namespace/id/version 三段字符串服务端询问客户端已加载的资源包
//Vanilla 默认 minecraft namespace
public sealed record KnownPack(string Namespace, string Id, string Version)
{
    //VanillaNamespace 原版 namespace 常量
    public const string VanillaNamespace = "minecraft";

    //Vanilla 创建 minecraft namespace 的 KnownPack 用当前版本号
    public static KnownPack Vanilla(string id)
        => new(VanillaNamespace, id, SharedConstants.Version);

    //IsVanilla 是否 minecraft namespace
    public bool IsVanilla => Namespace == VanillaNamespace;

    public override string ToString() => $"{Namespace}:{Id}:{Version}";
}
