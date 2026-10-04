namespace NetCraft.Registry;

//TODO packs阶段补全KnownPack记录资源包来源
public sealed record KnownPack(string Namespace, string Id, string Version);
