namespace NetCraft.Gpu;

//DeviceLimits GPU hardware limits, maps to vanilla DeviceLimits
//MaxTextureSize queried by GuiItemAtlas.ComputeTextureSizeFor instead of the hardcoded 4096
//MinUniformBufferOffsetAlignment used by Lighting for UBO slice alignment, ignorable when a single UBO is used
public sealed record DeviceLimits(int MaxTextureSize, int MinUniformBufferOffsetAlignment = 1)
{
    //MaxTextureSizeForFormat returns the max texture size for a GpuImageFormat
    //maps to vanilla Integer.highestOneBit(min(maxTextureSize, sqrt(maxMemoryAllocationSize/blockSize)))
    //NetCraft does not expose maxMemoryAllocationSize, so this simply returns MaxTextureSize
    public int MaxTextureSizeForFormat(GpuImageFormat format) => MaxTextureSize;
}
