using NetCraft.Registry;

namespace NetCraft.Storage;

//区域存储元信息对应原版RegionStorageInfo
//记录关卡名维度类型，用于日志和外部文件定位
public sealed record RegionStorageInfo(string Level, ResourceKey<Level> Dimension, string Type)
{
    //追加类型后缀生成派生info，用于同一维度的多种region文件
    public RegionStorageInfo WithTypeSuffix(string suffix) => new(Level, Dimension, Type + suffix);
}
