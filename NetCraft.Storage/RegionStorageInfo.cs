using NetCraft.Registry;

namespace NetCraft.Storage;

//Region storage metadata, maps to vanilla RegionStorageInfo
//Records level name, dimension and type, used for logging and locating external files
public sealed record RegionStorageInfo(string Level, ResourceKey<Level> Dimension, string Type)
{
    //Append a type suffix to derive a new info, for multiple region file kinds in the same dimension
    public RegionStorageInfo WithTypeSuffix(string suffix) => new(Level, Dimension, Type + suffix);
}
