namespace NetCraft.Registry;

//TODO fill in KnownPack during the packs stage to record the source resource pack
public sealed record KnownPack(string Namespace, string Id, string Version);
