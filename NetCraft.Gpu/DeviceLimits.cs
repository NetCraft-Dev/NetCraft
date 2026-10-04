namespace NetCraft.Gpu;

//DeviceLimits GPU 设备硬件限制对标原版 DeviceLimits
//MaxTextureSize 供 GuiItemAtlas.ComputeTextureSizeFor 查询替代硬编码 4096
//MinUniformBufferOffsetAlignment 供 Lighting UBO 切片对齐用单 UBO 场景可忽略
public sealed record DeviceLimits(int MaxTextureSize, int MinUniformBufferOffsetAlignment = 1)
{
    //MaxTextureSizeForFormat 按 GpuImageFormat 返回最大纹理尺寸
    //对标原版 Integer.highestOneBit(min(maxTextureSize, sqrt(maxMemoryAllocationSize/blockSize)))
    //NetCraft 未暴露 maxMemoryAllocationSize 简化为直接返回 MaxTextureSize
    public int MaxTextureSizeForFormat(GpuImageFormat format) => MaxTextureSize;
}
