using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Vulkan;

//VulkanTextureView view over a VulkanImage, maps to vanilla VulkanGpuTextureView
//The underlying VkImageView is owned by VulkanImage; closing the view does not destroy it
public sealed unsafe class VulkanTextureView : GpuTextureView
{
    private readonly VulkanImage _image;
    private bool _disposed;

    public VulkanImage Image => _image;

    internal VulkanTextureView(VulkanImage image, int baseMipLevel, int mipLevels) : base(image, baseMipLevel, mipLevels)
    {
        _image = image;
    }

    public override bool IsClosed => _disposed;

    public override void Dispose() => _disposed = true;
}
