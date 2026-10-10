using NetCraft.Codec;

namespace NetCraft.Resources;

//MetadataSectionType metadata section descriptor, maps to vanilla net.minecraft.server.packs.metadata.MetadataSectionType
//Pairs a section name with the codec that reads that section out of pack.mcmeta
public sealed record MetadataSectionType<T>(string Name, Codec<T> ValueCodec);
