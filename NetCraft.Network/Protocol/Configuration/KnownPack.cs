using NetCraft.Config;

namespace NetCraft.Network.Protocol.Configuration;

//KnownPack known resource pack entry, maps to vanilla net.minecraft.server.packs.repository.KnownPack
//Contains the three strings namespace/id/version; the server asks the client which resource packs are loaded
//Vanilla the default minecraft namespace
public sealed record KnownPack(string Namespace, string Id, string Version)
{
    //VanillaNamespace the vanilla namespace constant
    public const string VanillaNamespace = "minecraft";

    //Vanilla creates a KnownPack in the minecraft namespace using the current version
    public static KnownPack Vanilla(string id)
        => new(VanillaNamespace, id, SharedConstants.Version);

    //IsVanilla whether it is the minecraft namespace
    public bool IsVanilla => Namespace == VanillaNamespace;

    public override string ToString() => $"{Namespace}:{Id}:{Version}";
}
